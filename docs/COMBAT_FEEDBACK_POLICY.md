# Combat Feedback Policy — Forbes AI Fusion Prototype

## Pipeline

```
Authoritative damage result
    Health.DealDamageRpc (runs on State Authority only)
        │
        ├─ Writes: NetworkedHealth (HP reduced)
        ├─ Writes: LastHitDamage    (raw damage value — written before seq++)
        ├─ Writes: LastHitTick      (simulation tick — written before seq++)
        └─ Writes: LastHitEventSeq++ (monotonic byte counter — incremented last)
                │
                └─► Replicated to all clients via Fusion [Networked] props
                        │
                        └─► OnChangedRender(LastHitEventSeq) fires Health.CombatHitReceived(damage)
                                │
                                └─► HitImpactView (MonoBehaviour on the target prefab)
                                        └─► FloatingCombatTextCanvas.ShowDamage(transform, damage)
                                                └─► FloatingCombatTextItem (screen-space UI)
                                                        └─► animated upward in pixels, fades out
```

## Authority rules

- The three event fields (`LastHitEventSeq`, `LastHitDamage`, `LastHitTick`) are written
  **only on State Authority** inside `Health.DealDamageRpc`.
- No client, cosmetic component, or RPC other than `DealDamageRpc` may write these fields.
- `DealDamageRpc` is guarded by `HasStateAuthority` and `IsDead` checks, so the event fires
  **only when damage is genuinely applied** — never on a rejected call.

## The hit effect is not gameplay

`HitImpactView` observes `Health.CombatHitReceived` and produces a **local-only** visual
(a floating screen-space damage number above the target). This effect:

- Is rendered as **screen-space UI** (ScreenSpaceOverlay Canvas), **not** a world-space
  TextMesh. Text size and position are fixed in screen pixels and do not depend on
  camera distance or perspective projection.
- Has **no physics collider**. `FloatingCombatTextItem` uses only a `RectTransform` and
  UI components; it is parented to the `FloatingCombatTextCanvas` overlay, which carries
  `CanvasGroup.blocksRaycasts = false`.
- Has **no effect** on any game state — HP, death, targeting, cooldowns, or scores.
- Is **not networked**: each client spawns its own text independently when the replicated
  `LastHitEventSeq` arrives. Numbers may appear at slightly different times on different
  clients; this is acceptable for cosmetic feedback.
- May be **removed or replaced** entirely without affecting any gameplay outcome.

## Floating damage text — screen-space design

`FloatingCombatTextCanvas` owns a single persistent `ScreenSpaceOverlay` Canvas
(created lazily on the first hit; `DontDestroyOnLoad`). For each hit it spawns a
`FloatingCombatTextItem` child:

1. **Anchor** is computed by `FloatingCombatTextLogic.GetWorldAnchor(transform)`:
   - collider present and enabled → `bounds.max.y + 0.25 m`
   - no collider → `position + Vector3.up * 2.3 m`
2. **Screen projection**: every `LateUpdate`, the world anchor is projected via
   `Camera.main.WorldToScreenPoint`. If the target is behind the camera (`z ≤ 0`),
   the last valid position is kept unchanged.
3. **Animation**: upward pixel offset (`FloatingCombatTextLogic.ComputePixelOffset`)
   follows an ease-out curve over the item's 0.9 s lifetime (max 80 px rise).
4. **Fade**: alpha (`FloatingCombatTextLogic.ComputeAlpha`) stays at 1.0 for the first
   60 % of lifetime, then linearly fades to 0.
5. **Self-destruct**: `FloatingCombatTextItem` calls `Destroy(gameObject)` after lifetime.

`FloatingCombatTextCanvas.ShowDamage` is a no-op when `Camera.main` is null (headless
servers, EditMode test environments).

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

## Collider rule (cosmetic visuals)

Cosmetic visual objects **must never have an active physics collider**.
`GameObject.CreatePrimitive` always attaches a collider. `Destroy(collider)` is deferred to
end-of-frame; the collider remains live for one FixedUpdate step and can deflect
`CharacterController.Move()` on other objects.

**Required pattern** (used in `SpellImpactView`, `ActiveSpellInstancePresenter`):

```csharp
if (go.TryGetComponent<Collider>(out var col)) {
    col.enabled = false;  // synchronous — removes from physics immediately
    Destroy(col);          // deferred — memory only
}
```

`FloatingCombatTextItem` is a pure RectTransform hierarchy inside a Canvas; it never
attaches a Collider and is exempt from this rule.

## Adding HitImpactView to prefabs

`HitImpactView` is a plain `MonoBehaviour` and must be manually added to prefabs:

- **PlayerCharacter.prefab** — so players see a number when they take damage.
- **TrainingDummy.prefab** — so the dummy shows damage when hit.

`[RequireComponent(typeof(Health))]` is declared on the class; Unity will enforce this
in the Inspector when the component is added.

## What this is not

- **Not a combat log.** There is no event history, no event queue, no per-frame dispatch.
- **Not a gameplay signal.** Nothing driven by `CombatHitReceived` may affect HP, targeting,
  or cooldowns.
- **Not a projectile system.** Spell instances and projectile non-goals are in [architecture.md](architecture.md) (Spell system).
- **Not a full spell feedback system.** Per-spell cosmetic differences (e.g. fire vs. frost
  hit color) should read from the existing `RpcOnSpellImpact` path in
  `NetworkCombatController`, not from the health event.

## Future extensions

To add hit sounds, screen shake, or additional visual variants, add new `MonoBehaviour`
observers that subscribe to `Health.CombatHitReceived`:

```csharp
_health.CombatHitReceived += damage => PlayHitSound(damage);
```

Do not add new `[Networked]` fields to `Health` for purely visual variations.
Use `LastHitDamage` for local interpolation and cosmetic logic.

To add per-spell damage number coloring or icons, read from the `RpcOnSpellImpact`
callback in `NetworkCombatController`; do not extend the `Health` hit event.
