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

`NetworkCombatController` currently owns the prototype missile state. This is a single-slot, single-caster design intended for early vertical-slice iteration.

### Missile lifecycle

1. **Release** — when a projectile spell cast resolves (`ResolveCast` or instant), the combat controller arms a missile slot: `PendingMissileReleaseTick` is set and the initial virtual position (`_missileVirtualPos`) is placed at the caster.
2. **Advance** — each `FixedUpdateNetwork` tick (State Authority only), `SpellTravelLogic.AdvanceMissilePosition` moves `_missileVirtualPos` toward the target's *current* position by `spell.ProjectileSpeed * Runner.DeltaTime`.
3. **Impact** — when `SpellTravelLogic.HasMissileArrived` returns true (virtual position within arrival threshold of the target), the missile slot is cleared and damage is dispatched.
4. **Damage resolution** — `Health.DealDamageRpc` is called by State Authority; the same authority-guarded path used by instant spells and melee.

### Moving-target behavior

Because the missile advances toward the target's *current* position each tick, a moving target changes the missile's flight path and therefore its flight duration. A target that moves away takes longer to hit; a target that moves toward the caster is hit sooner. This is intentional WoW-style homing behavior for the prototype.

### What is not rechecked during flight

Range and line-of-sight are **not** rechecked after the missile is released. If these checks are needed during flight they must be added explicitly; they are not implied by the current model.

---

## 3. Current limitations

- **One missile slot per combat controller.** `NetworkCombatController` holds a single in-flight missile. Firing a second projectile spell while one is already in flight is not supported in the current prototype.
- **Not a final architecture.** The single-slot missile inside `NetworkCombatController` is prototype scaffolding, not the intended long-term home for missile state.
- **`_missileVirtualPos` is non-networked and authority-only.** Clients do not receive the missile position as networked state. Any future cosmetic visual must derive position from replicated parameters (origin, target, tick), not from this field.
- **Authority transfer during in-flight missile is only prototype-safe.** If state authority transfers while a missile is in flight, the missile state (non-networked) will be lost. This is acceptable for the prototype but must be addressed before shipping.
- **No `TargetedMissileManager`.** A dedicated world-level manager for multiple simultaneous missiles does not exist yet.
- **Cosmetic projectile visual is minimal.** `CosmeticProjectileView` (plain `MonoBehaviour` on the player prefab) shows a small orange sphere that lerps from the caster toward the target while `PendingImpactSpellId != 0`. It reads only the three already-replicated `[Networked]` properties (`PendingImpactSpellId`, `PendingImpactTarget`, `PendingMissileReleaseTick`) and never writes networked state or applies damage. The visual position is approximate (lerp from current caster position, not release-tick position) — acceptable for a cosmetic indicator.
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

When NPC casters, multiple simultaneous in-flight missiles, better visual synchronization, or pooling are required, the missile state should migrate out of `NetworkCombatController` into a dedicated **`TargetedMissileManager`** (world-level authority component). Key considerations for that transition:

- Each in-flight missile becomes an entry in a managed collection, not an inline field on the combat controller.
- Replicated missile parameters (origin, target `NetworkId`, release tick, spell id, speed) allow clients to reconstruct a cosmetic visual without a non-networked position field.
- Cosmetic visuals are spawned locally by clients from that replicated state; they remain client-local and never influence gameplay outcomes.
- Pooling and multi-missile support are natural extensions of the manager pattern.

---

## 6. Testing policy

| Layer | What it covers |
|-------|---------------|
| **EditMode — `SpellTravelLogicTests`** | Pure missile math: `AdvanceMissilePosition`, `HasMissileArrived`, speed/distance/threshold arithmetic. No runner required. Tests here must not depend on `MonoBehaviour` lifecycle or Fusion. |
| **PlayMode smokes** | Moving-target runtime behavior: missile released on cast resolution, advances per tick, impacts a moving target, damage applied by State Authority. These tests run via `GameMode.Single` and exercise the full `FixedUpdateNetwork` path. |
| **Future cosmetic visual tests** | Must be kept **strictly separate** from gameplay missile tests. A cosmetic visual test must never assert damage values, HP changes, or authoritative state. If a cosmetic visual test can only pass by having the visual apply damage, the test is wrong. |

EditMode tests are the primary regression guard for `SpellTravelLogic` math changes. PlayMode smokes are the primary guard for authority-correctness and moving-target behavior.
