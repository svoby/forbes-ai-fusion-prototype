# Combat Feedback Policy — Forbes AI Fusion Prototype

## Pipeline

```
Authoritative damage result
    Health.DealDamageRpc (runs on State Authority only)
        │
        ├─ Writes: NetworkedHealth (HP reduced)
        ├─ Writes: LastHitEventSeq++ (monotonic byte counter)
        ├─ Writes: LastHitDamage    (raw damage value)
        └─ Writes: LastHitTick      (simulation tick)
                │
                └─► Replicated to all clients via Fusion [Networked] props
                        │
                        └─► OnChangedRender(LastHitEventSeq) fires Health.CombatHitReceived(damage)
                                │
                                └─► HitImpactView (MonoBehaviour on the target prefab)
                                        └─► Spawns a brief local-only red flash — no gameplay outcome
```

## Authority rules

- The three event fields (`LastHitEventSeq`, `LastHitDamage`, `LastHitTick`) are written
  **only on State Authority** inside `Health.DealDamageRpc`.
- No client, cosmetic component, or RPC other than `DealDamageRpc` may write these fields.
- `DealDamageRpc` is guarded by `HasStateAuthority` and `IsDead` checks, so the event fires
  **only when damage is genuinely applied** — never on a rejected call.

## The hit effect is not gameplay

`HitImpactView` observes `Health.CombatHitReceived` and produces a **local-only** visual
(a brief colored flash at the target's center mass). This effect:

- Has **no physics collider** (synchronously disabled per the collider rule below).
- Has **no effect** on any game state — HP, death, targeting, cooldowns, or scores.
- Is **not networked**: each client spawns its own flash independently when the replicated
  `LastHitEventSeq` arrives. The flashes may appear at slightly different times on different
  clients; this is acceptable for cosmetic feedback.
- May be **removed or replaced** entirely without affecting any gameplay outcome.

## Event fields

| Field              | Type    | Set by          | Purpose                                          |
|--------------------|---------|-----------------|--------------------------------------------------|
| `LastHitEventSeq`  | `byte`  | State Authority | Monotonic counter; `OnChangedRender` → cosmetic  |
| `LastHitDamage`    | `float` | State Authority | Raw requested damage (for cosmetic display)      |
| `LastHitTick`      | `int`   | State Authority | Simulation tick of the impact                    |

`LastHitEventSeq` wraps at 256. This is acceptable for a cosmetic trigger; the counter is
not used for any gameplay decision and a single display frame with a stale value is harmless.

## Trigger points

`DealDamageRpc` is the single authoritative damage entry point, called from exactly two paths:

1. **Spell impact** — called from `NetworkCombatController` after validation, for both instant
   spells and projectile spells when the logical missile arrives (via `TryResolvePendingImpact`).
2. **Mob melee** — called from `NetworkMobBrain.TryMeleeAuthority` when attack range and
   attack-interval cooldown allow.

No cosmetic object or client-side code calls `DealDamageRpc`. The event therefore fires
if and only if damage was genuinely applied by State Authority.

## Collider rule (same as PROJECTILE_POLICY.md §4a)

Cosmetic visual objects **must never have an active physics collider**.
`GameObject.CreatePrimitive` always attaches a collider. `Destroy(collider)` is deferred to
end-of-frame; the collider remains live for one FixedUpdate step and can deflect
`CharacterController.Move()` on other objects.

**Required pattern** (already used in `HitImpactView`, `SpellImpactView`, `CosmeticProjectileView`):

```csharp
if (go.TryGetComponent<Collider>(out var col)) {
    col.enabled = false;  // synchronous — removes from physics immediately
    Destroy(col);          // deferred — memory only
}
```

## Adding HitImpactView to prefabs

`HitImpactView` is a plain `MonoBehaviour` and must be manually added to prefabs:

- **PlayerCharacter.prefab** — so players see a flash when they take damage.
- **TrainingDummy.prefab** — so the dummy flashes when hit.

`[RequireComponent(typeof(Health))]` is declared on the class; Unity will enforce this
in the Inspector when the component is added.

## What this is not

- **Not a combat log.** There is no event history, no event queue, no per-frame dispatch.
- **Not a gameplay signal.** Nothing driven by `CombatHitReceived` may affect HP, targeting,
  or cooldowns.
- **Not a projectile system.** Projectile policy is covered in [PROJECTILE_POLICY.md](PROJECTILE_POLICY.md).
- **Not a full spell feedback system.** Per-spell cosmetic differences (e.g. fire vs. frost
  hit color) should read from the existing `RpcOnSpellImpact` path in
  `NetworkCombatController`, not from the health event.

## Future extensions

If floating damage numbers, hit sounds, or screen shake are added, add new
`MonoBehaviour` observers that subscribe to `Health.CombatHitReceived`:

```csharp
_health.CombatHitReceived += damage => ShowDamageNumber(damage);
```

Do not add new `[Networked]` fields to `Health` for purely visual variations.
Use `LastHitDamage` for local interpolation and cosmetic logic.
