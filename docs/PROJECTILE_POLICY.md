# Projectile Policy — Forbes AI Fusion Prototype

## 1. Terminology

| Term | Definition |
|------|-----------|
| **authoritative targeted missile** | Gameplay missile state owned exclusively by State Authority. Advances per simulation tick, resolves impact, applies damage. This is the only missile path that matters for gameplay outcomes. |
| **cosmetic projectile visual** | A local client visual object that approximates missile position during in-flight spells. Never applies damage; must never own or influence gameplay state. Implemented as `CosmeticProjectileView` (plain `MonoBehaviour` on the player prefab). |

These two concepts must remain permanently separated. Any visual object that moves toward a target is cosmetic only; the authoritative missile is the sole source of truth for damage.

---

## 2. Current runtime model

### Ownership

`PlayerMissileSlot` owns the prototype missile state. It is a `NetworkBehaviour` sibling on the player prefab (`[DefaultExecutionOrder(-200)]`), separate from `NetworkCombatController` (`-100`). `NetworkCombatController` calls `_missileSlot.Schedule(...)` and `_missileSlot.Clear()` and subscribes to `OnImpact` / `OnCancelled` events for damage dispatch and feedback.

### Replicated travel state

The four `[Networked]` fields on `PlayerMissileSlot` fully describe any in-flight spell:

| Field | Purpose |
|-------|---------|
| `PendingImpactSpellId` | 0 = no missile; > 0 = spell id in flight |
| `PendingImpactTarget` | Target `NetworkId` locked at release |
| `PendingMissileReleaseTick` | Simulation tick at release; used for cosmetic timing |
| `MissileOrigin` | Caster world position at release; replicated so cosmetic views can reconstruct the correct launch arc without drift |

### Missile lifecycle

1. **Release** — when a projectile spell cast resolves (`ResolveCast` or instant), `NetworkCombatController` calls `_missileSlot.Schedule(spellId, targetId, transform.position)`. The slot captures `MissileOrigin` at the caster's current position and initialises `_missileVirtualPos` there.
2. **Advance** — each `FixedUpdateNetwork` tick (State Authority only, inside `PlayerMissileSlot`), `SpellTravelLogic.AdvanceMissilePosition` moves `_missileVirtualPos` toward the target's *current* position by `spell.ProjectileSpeed * Runner.DeltaTime`.
3. **Impact** — when `SpellTravelLogic.HasMissileArrived` returns true, `PlayerMissileSlot` clears the slot and fires `OnImpact`. `NetworkCombatController` handles the event: calls `Health.DealDamageRpc` then `RpcOnSpellImpact`.
4. **Damage resolution** — `Health.DealDamageRpc` is called by State Authority; the same authority-guarded path used by instant spells and melee.

### Moving-target behavior

Because the missile advances toward the target's *current* position each tick, a moving target changes the missile's flight path and therefore its flight duration. A target that moves away takes longer to hit; a target that moves toward the caster is hit sooner. This is intentional WoW-style homing behavior for the prototype.

### What is not rechecked during flight

Range and line-of-sight are **not** rechecked after the missile is released. If these checks are needed during flight they must be added explicitly; they are not implied by the current model.

---

## 3. Current limitations

- **One missile slot per caster.** `PlayerMissileSlot` holds a single in-flight missile. Firing a second projectile spell while one is already in flight silently overwrites the first (logged as a warning). This is an acknowledged prototype limitation.
- **`_missileVirtualPos` is non-networked and authority-only.** Clients do not receive the missile position as networked state. Cosmetic visuals derive their position from the four replicated parameters (`MissileOrigin`, `PendingImpactTarget`, `PendingMissileReleaseTick`, `PendingImpactSpellId`), not from this field.
- **Authority transfer during in-flight missile is only prototype-safe.** If state authority transfers while a missile is in flight, `_missileVirtualPos` is re-initialised to the caster's current position in `Spawned()`. The homing path resumes correctly from there; no damage is lost, but the virtual position jumps.
- **No `TargetedMissileManager`.** A dedicated world-level manager for multiple simultaneous missiles does not exist yet.
- **Cosmetic projectile visual is minimal.** `CosmeticProjectileView` (plain `MonoBehaviour`, `[RequireComponent(typeof(PlayerMissileSlot))]`) shows a small orange sphere that lerps from `MissileOrigin` toward the target while `PendingImpactSpellId != 0`. The lerp uses the replicated release position so the visual arc is correct even when the caster moves after firing.
- **No pooling.** Missile "objects" are inline fields; no pool or collection management exists.
- **No miss/resist/immune combat-result logic.** All missiles that arrive apply full damage. Miss chances, resistances, and immune states are out of scope for the prototype.

---

## 4. Explicit non-goals

The following approaches are **not** part of this project's projectile design for normal targeted spells:

- **Counter-Strike-style instant-travel hitscan bullets.** Targeted spell damage is not resolved at the moment of cast by a hitscan.
- **Collider/raycast projectile damage.** Normal targeted spells do not use physics colliders, trigger volumes, or raycasts to detect impact.
- **`Runner.Spawn` projectile `NetworkObject`s.** Normal targeted spells do not spawn a networked `NetworkObject` that travels through the world. The missile is a virtual state slot, not a scene object.
- **Cosmetic visuals applying damage.** Under no circumstances may a cosmetic projectile visual participate in damage resolution or authoritative combat state.

---

## 4a. Cosmetic visual — collider rule

**Cosmetic visual objects must never have an active physics collider.**

`GameObject.CreatePrimitive` (used for placeholder spheres in `CosmeticProjectileView` and `SpellImpactView`) always attaches a collider as a Unity side effect — even though we never asked for one. `Destroy(collider)` is **deferred to end-of-frame**: the collider remains live for the entire physics step in which the object is created, and during that step `CharacterController.Move()` will deflect off it, producing a visible jump on the target.

**Rule:** whenever a cosmetic primitive is created, disable the collider **synchronously** before doing anything else:

```csharp
if (go.TryGetComponent<Collider>(out var col)) {
    col.enabled = false;  // synchronous — removes from physics immediately
    Destroy(col);          // deferred cleanup — memory only
}
```

Do not rely on `Destroy` alone. The `enabled = false` line is mandatory and must come first. This applies to every cosmetic `GameObject.CreatePrimitive` call in this codebase, present and future.

---

## 5. Long-term direction

`PlayerMissileSlot` is the current home for single-caster missile state. When NPC casters, multiple simultaneous in-flight missiles, better visual synchronisation, or pooling are required, the next step is a world-level **`TargetedMissileManager`**. Key considerations for that transition:

- Each in-flight missile becomes an entry in a managed collection (`NetworkLinkedList` or similar), not a single-slot field on a per-caster component.
- The four travel-description fields already replicated on `PlayerMissileSlot` (`MissileOrigin`, `PendingImpactTarget`, `PendingMissileReleaseTick`, `PendingImpactSpellId`) define the correct minimal wire format — replicate the same fields per-entry in the manager.
- Cosmetic visuals are spawned locally by clients from that replicated state; they remain client-local and never influence gameplay outcomes.
- Pooling and multi-missile support are natural extensions of the manager pattern.

---

## 6. Testing policy

| Layer | What it covers |
|-------|---------------|
| **EditMode — `SpellTravelLogicTests`** | Pure missile math: `AdvanceMissilePosition`, `HasMissileArrived`, speed/distance/threshold arithmetic. No runner required. Tests here must not depend on `MonoBehaviour` lifecycle or Fusion. |
| **EditMode — `PlayerMissileSlotTests`** | Structural: execution-order attribute, `NetworkBehaviour` inheritance, `CosmeticProjectileView` require-component contract. `[Networked]` property behaviour is not tested here (requires a live runner). |
| **PlayMode smokes** | Moving-target runtime behavior: missile released on cast resolution, advances per tick, impacts a moving target, damage applied by State Authority, `MissileOrigin` set on schedule and cleared on impact. These tests run via `GameMode.Single` and exercise the full `FixedUpdateNetwork` path. |
| **Future cosmetic visual tests** | Must be kept **strictly separate** from gameplay missile tests. A cosmetic visual test must never assert damage values, HP changes, or authoritative state. If a cosmetic visual test can only pass by having the visual apply damage, the test is wrong. |

EditMode tests are the primary regression guard for `SpellTravelLogic` math changes. PlayMode smokes are the primary guard for authority-correctness and moving-target behavior.
