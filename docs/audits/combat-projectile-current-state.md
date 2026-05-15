# Combat / Projectile Architecture — Current State Audit

**Scope:** Read-only audit of current `master` state. No code, test, prefab, or scene files
were modified. All citations are to files present on `master` at the time of this audit
(2026-05-15).

---

## 1. Current implemented flow

### 1a. Cast initiation

Player input arrives via `GameplayInput` (struct) captured in
`FusionInputProvider.OnInput` and forwarded to the Fusion tick.
`NetworkCombatController.FixedUpdateNetwork` (`Assets/Scripts/Combat/NetworkCombatController.cs`,
`[DefaultExecutionOrder(-100)]`) is the authoritative entry point on the player's own
machine (Shared Mode, so the player is their own State Authority). The flow:

```
KeyboardInputSource.Update
  └─► FusionInputProvider.OnInput → GameplayInput struct
        └─► NetworkCombatController.FixedUpdateNetwork (state authority)
              ├─ TickCombatRuntime()   — death / cast-timer expiry checks
              └─ ProcessPlayerInput()  — button dispatch → TryRequestCast / TryCancelCast
```

`TryRequestCast` (`NetworkCombatController.Cast.cs`) delegates to `TryStartCast`, which calls
`CombatValidator.TryValidate` (runner-aware overload, `CombatValidator.cs`). The validator
resolves the target via `runner.TryFindObject`, then applies ordered checks:
AlreadyCasting → GCD → Cooldown → NoTarget → TargetDead → OutOfRange.

### 1b. Instant spells (no cast time, e.g. Arcane Shot, Heavy Blast when `castTimeSec == 0`)

> Note: per `SpellRegistry`, Arcane Shot (`SpellId 2`) has `castTimeSec: 0f` and no
> projectile; Heavy Blast (`SpellId 3`) has `castTimeSec: 2.5f`. Only Spell 2 is a true
> instant — see §1c for cast-time spells.

For zero-cast-time, zero-projectile spells: GCD and cooldown are set immediately, then
`targetHealth.DealDamageRpc(spell.Damage)` is called directly, followed by
`DispatchImpactVisual` → `RpcOnSpellImpact` (RPC broadcast to all clients) →
`SpellImpactView.OnSpellImpact` (cosmetic sphere flash, `SpellImpactView.cs`).

### 1c. Cast-time spells (e.g. Fireball `SpellId 1`, Heavy Blast `SpellId 3`)

On a valid cast start, `NetworkCombatController.Cast.cs:TryStartCast` writes the four
networked cast-state fields (`CurrentSpellId`, `CastTarget`, `CastStartTick`, `CastEndTick`).
These are replicated to all clients and drive the cast bar UI
(`CastBarView.cs`, `NetworkCombatController.IsCasting`, `CastProgress`).

On the tick where `Runner.Tick >= CastEndTick`, `TickCombatRuntime` calls `ResolveCast`,
which re-validates the target (range/alive/exists checks) and either:
- **fails** → clears cast state, sets `LastCombatFeedbackReason`, no damage;
- **succeeds** → starts GCD + cooldown, then:
  - **projectile spell** → `ScheduleProjectileInstance` (enters `ActiveSpellInstanceRegistry`);
  - **non-projectile** → `DealDamageRpc` directly + `DispatchImpactVisual`.

Mid-cast movement (`Move.sqrMagnitude > 1e-6`) or Jump button cancels the cast silently
(no HUD feedback). `OutOfRange` during cast is intentionally _not_ a mid-cast cancel reason
(`IsMidCastCancelReason`, `NetworkCombatController.cs` line 224); range is re-checked at
resolution.

### 1d. Projectile spell flight (Fireball, `SpellId 1`)

After `ScheduleProjectileInstance` (`NetworkCombatController.Cast.cs`), an
`ActiveSpellInstance` struct (SpellId, Kind=TargetedProjectile, CasterId, TargetId, Origin,
ReleaseTick, InstanceId) is written into the `ActiveSpellInstanceRegistry`
(`Assets/Scripts/Combat/ActiveSpellInstanceRegistry.cs`, `[DefaultExecutionOrder(-200)]`).

The registry is a `NetworkBehaviour` with a fixed-capacity (16) `NetworkArray<ActiveSpellInstance>`.
Because the array is `[Networked]`, all four flight-descriptor fields are replicated to all
clients, enabling late-joining clients to reconstruct in-flight projectile visuals.

Per-tick homing (`FixedUpdateNetwork`, state authority only):

```
ActiveSpellInstanceRegistry.FixedUpdateNetwork   (before NCC at order -200)
  └─ for each active slot → TickInstance(i)
       ├─ target missing / dead → Complete(i) + OnInstanceCancelled event
       └─ AdvanceMissilePosition(_virtualPositions[i], targetPos, speed, dt)
            └─ HasMissileArrived? → Complete(i) + OnInstanceArrived event
```

`_virtualPositions` is a non-networked `Vector3[]` on state authority only; it holds
authoritative missile positions without replicating them. `SpellTravelLogic.AdvanceMissilePosition`
uses `Vector3.MoveTowards` (step = `speed * deltaTime`). `SpellTravelLogic.HasMissileArrived`
checks post-advance distance against the step with a 1e-5 relative tolerance
(`SpellTravelLogic.cs`).

### 1e. Damage dispatch on impact

When `OnInstanceArrived` fires, `NetworkCombatController.HandleInstanceArrived`
(`NetworkCombatController.cs`) looks up the spell and calls
`targetHealth.DealDamageRpc(spell.Damage)` + `DispatchImpactVisual`.

`Health.DealDamageRpc` (`Assets/Scripts/Player/Health.cs`) runs only on State Authority,
reduces `NetworkedHealth`, and writes the three combat event fields
(`LastHitEventSeq++`, `LastHitDamage`, `LastHitTick`) that propagate floating damage
numbers to all clients via `OnChangedRender` → `Health.CombatHitReceived` →
`HitImpactView` → `FloatingCombatTextCanvas.ShowDamage`.

### 1f. Cosmetic projectile visuals

`ActiveSpellInstancePresenter` (`Assets/Scripts/Combat/ActiveSpellInstancePresenter.cs`) is a
plain `MonoBehaviour` (not a `NetworkBehaviour`) on the same GameObject as the registry. In
`LateUpdate` it reads the replicated `Instances` array, creates a local `GameObject` visual per
active `TargetedProjectile` instance (keyed by `InstanceId`, not index), and advances visual
positions using the same `SpellTravelLogic.AdvanceMissilePosition` step model with
`Time.deltaTime` (render-rate, not tick-rate). Position reconstruction for late-joining clients
fast-forwards from `Origin` through `(Runner.Tick - ReleaseTick)` elapsed ticks.

Collider removal follows the documented required pattern (`col.enabled = false; Destroy(col)`)
for both the prefab path and the primitive sphere fallback.

### 1g. Cooldown management

`NetworkCombatController.Cooldowns.cs` holds three hardcoded per-spell networked int fields
(`Cooldown1EndTick`, `Cooldown2EndTick`, `Cooldown3EndTick`) and a `GcdEndTick` field on the
main class. All are tick-based. `SecsToTicks` uses `Mathf.CeilToInt(seconds * tickRate)`.

---

## 2. Docs vs implementation

### Confirmed matches

| Doc claim | Implementation |
|-----------|----------------|
| `ActiveSpellInstanceRegistry` at `[DefaultExecutionOrder(-200)]`, before NCC at `-100` | Confirmed: `ActiveSpellInstanceRegistry.cs` line 18, `NetworkCombatController.cs` line 19. |
| Registry: fixed 16-slot `NetworkArray`, replicated | Confirmed: `ActiveSpellInstanceRegistry.cs` lines 22–23. |
| `_virtualPositions` is non-networked, authority-only | Confirmed: `ActiveSpellInstanceRegistry.cs` line 25. |
| Projectile damage via `OnInstanceArrived` → `DealDamageRpc` | Confirmed: `NetworkCombatController.cs` `HandleInstanceArrived`. |
| `DealDamageRpc` guarded by `HasStateAuthority` and `IsDead` | Confirmed: architecture doc and `COMBAT_FEEDBACK_POLICY.md`. |
| Cosmetic visuals have no collider (sync disable + deferred Destroy) | Confirmed: `ActiveSpellInstancePresenter.CreatePrimitiveSphereVisual` and `CreateProjectileVisual` (prefab path uses `GetComponentsInChildren<Collider>()`; primitive path uses `TryGetComponent`). |
| `AdvanceMissilePosition` + `HasMissileArrived` are the active resolution mechanism | Confirmed: `SpellTravelLogic.cs` XML docs and `ActiveSpellInstanceRegistry.TickInstance`. |
| `ComputeTravelTicks` / `ComputeImpactTick` are retained but NOT called by NCC | Confirmed: `SpellTravelLogic.cs` XML docs note this explicitly. |
| Mob melee: `DealDamageRpc` via `NetworkMobBrain.TryMeleeAuthority` | Cited in `COMBAT_FEEDBACK_POLICY.md`; `NetworkMobBrain.cs` not read in this audit but assumed consistent. |
| `RpcOnSpellImpact` source = StateAuthority, targets = All | Confirmed: `NetworkCombatController.Feedback.cs` line 26. |
| `InstanceId` guards against visual reuse on index reuse | Confirmed: `ActiveSpellInstance.cs` and `ActiveSpellInstancePresenter.cs` dictionary keyed by `InstanceId`. |

### Confirmed mismatches / gaps

1. **`SpellRegistry` vs. architecture description — Arcane Shot is instant but non-projectile:**
   `docs/architecture.md` does not describe individual spell data; `SpellRegistry.cs` shows
   Spell 2 (Arcane Shot, `castTimeSec: 0f`, `projectileSpeedMetersPerSecond: 0f`) is an
   instant non-projectile that calls `DealDamageRpc` directly and Spell 3 (Heavy Blast,
   `castTimeSec: 2.5f`, `projectileSpeedMetersPerSecond: 0f`) is cast-time non-projectile.
   Neither doc nor comment describes the distinct path for "instant + has-projectile" vs
   "instant + no-projectile". The code handles both (`TryStartCast`, lines 99–104), but the
   docs don't call this out. Not a bug; a documentation gap.

2. **`CombatFailReason.CasterDead` (value 7) is pinned but never emitted:**
   `CombatFailReason.cs` documents this explicitly ("No production code path emits this value
   today"). Runtime caster-death goes through `CastCancelReason.Death` instead, which maps to
   `CombatFeedbackReason.CastInterruptedByDeath`. The wire value is reserved for future use.
   Not a bug; known and documented.

3. **`CombatFeedbackReason.CastInterruptedByMovement` (value 8) and
   `CastInterruptedByJump` (value 9) are reserved wire slots that are never set:**
   Both are documented as "not used — movement/jump cancel has no player feedback."
   `TryCancelCast` silently returns for `Movement` and `Jump` without writing to
   `LastCombatFeedbackReason`. Consistent with the enum comment; no mismatch.

4. **`ActiveSpellInstancePresenter.Start` uses `FindFirstObjectByType<NetworkRunner>()`:**
   `Start()` is called once at component activation, not in a hot path. However, this is a
   scene-wide object search deferred to startup rather than an injected or cached reference.
   The `AGENTS.md` rule about `FindObjectsByType` applies to "hot simulation paths"; `Start`
   is not hot. Flagged here as a minor architecture note (not a confirmed doc mismatch).

5. **Prefab collider removal for instantiated prefabs uses `GetComponentsInChildren<Collider>()`
   (plural, deep), while the primitive path uses `TryGetComponent` (singular, root only):**
   `COMBAT_FEEDBACK_POLICY.md` describes the required pattern for the primitive sphere case
   (synchronous `col.enabled = false; Destroy(col)`). For instantiated prefabs,
   `ActiveSpellInstancePresenter.CreateProjectileVisual` uses `Destroy(col)` without first
   disabling the collider. This is a subtle divergence from the documented "synchronous disable"
   pattern; `Destroy` is deferred, so any collider on an instantiated prefab survives one
   FixedUpdate. The `_projectileVisuals` array is currently empty in the inspector default, so
   this code path is not exercised in practice. Flagged as a latent issue if prefab-based
   projectile visuals are ever configured.

### Items the docs mention but were not read in this audit

- `Assets/Scripts/Player/Health.cs` — referenced in docs and tested in PlayMode fixtures; not
  read directly. Assumed consistent with `COMBAT_FEEDBACK_POLICY.md` description.
- `Assets/Scripts/Combat/SpellImpactView.cs`, `SpellVisualColors.cs` — cosmetic-only;
  not in the required context bundle for this audit.
- `Assets/Scripts/Mobs/NetworkMobBrain.cs` — mob melee path cited in policy doc; not
  in this audit's required scope.

---

## 3. Risk areas / stale concepts

### R1 — `ComputeTravelTicks` / `ComputeImpactTick` are retained but inactive

`SpellTravelLogic.ComputeTravelTicks` and `ComputeImpactTick` are explicitly documented as
"retained as a utility and for EditMode tests. **Not called by `NetworkCombatController`.**"
(`SpellTravelLogic.cs`). They have EditMode test coverage (`SpellTravelLogicTests.cs`). The
risk is that a future developer may assume these functions drive impact timing. The inline
XML comments already mitigate this, but the mismatch between "has EditMode tests" and "not
actually used in production simulation" is worth noting.

### R2 — Hardcoded three-slot cooldown switch/match in `Cooldowns.cs`

`GetCooldownEndTick` and `SetCooldownEndTick` (`NetworkCombatController.Cooldowns.cs`) use
explicit `switch` statements over spell IDs 1–3 mapping to three hardcoded `[Networked]` int
fields. Adding a fourth spell requires adding both a networked field and a new case in both
methods. There is no enforcement (no assertion, no compile error) when a spell ID falls outside
the handled range — it silently returns `0` or is a no-op. This is the most likely source of a
silent bug if the spell table grows.

### R3 — `ActiveSpellInstancePresenter.Start` uses a scene-wide runner search

`FindFirstObjectByType<NetworkRunner>()` in `Start()` is not in a hot path (called once), but
it is a fragile coupling: if the presenter activates before the runner spawns, `_runner` will
be null and visuals will silently not render. There is no retry, no late-binding, and no
logged warning when `_runner` is null in `LateUpdate`. This is acceptable for a prototype but
would break under any spawn-order race condition.

### R4 — Prefab-based projectile visual path has untested collider removal

`CreateProjectileVisual` (prefab branch) uses `Destroy(col)` without `col.enabled = false`.
The `_projectileVisuals` serialized array is empty by default, so this path is never exercised
in the current project. If a designer adds a prefab visual entry in the inspector, the
instantiated prefab's collider would survive one FixedUpdate tick and could deflect
`CharacterController.Move()`.

### R5 — No validation that `NetworkCombatController.HandleInstanceArrived` is only called for self-owned instances

`HandleInstanceArrived` early-returns if `instance.CasterId != Object.Id`, which is correct.
However, `_registry` subscribes to `OnInstanceArrived` for _all_ instances in the registry
(there is one registry per caster, co-located on the same GameObject, so the registry only
holds that caster's instances). The early-return guard is therefore redundant as currently
structured, but it would become load-bearing if the registry were ever shared across casters.
No bug today; subtle coupling worth documenting.

### R6 — `ActiveSpellInstance` has only one `SpellInstanceKind` (`TargetedProjectile = 0`)

The `Kind` field and `SpellInstanceKind` enum exist to allow future routing for different spell
types (e.g. AoE ground-targeted, instant hitscan). Currently there is only one value and the
presenter always checks `HasProjectile(spell)` rather than the `Kind` field. The Kind field is
replicated but not read by any production logic for branching. Stale extensibility scaffolding
that adds complexity without current value; acceptable for a prototype.

---

## 4. Recommended next safe slice

**Slice: Replace the hardcoded three-slot cooldown array with a fixed-size indexed
`NetworkArray<int>` keyed by spell slot (0-based), matching `SpellRegistry.All.Length`.**

Rationale:
- R2 is the highest concrete risk: the switch/match cooldown dispatch is the most likely to
  cause a silent production bug when the spell table grows.
- The change is contained to `NetworkCombatController.cs` (remove three `[Networked]` fields,
  add one `[Networked, Capacity(N)] NetworkArray<int>`), `NetworkCombatController.Cooldowns.cs`
  (replace switch with index lookup), and the relevant EditMode tick-math tests.
- No new concepts, no authority model changes, no new public API beyond what already exists.
- The slice is non-overlapping with any other named feature (mob AI, visual polish, new spells).
- It makes the cooldown capacity constraint visible and compile-time verifiable rather than
  silent.

This slice should be filed as a separate follow-up issue.

---

## 5. Acceptance criteria for the next slice

1. `NetworkCombatController` declares no individual `CooldownNEndTick` fields; all per-spell
   cooldowns are stored in a single `[Networked, Capacity(SpellRegistry.All.Length)]
   NetworkArray<int>` (or equivalent bounded array type).
2. `GetCooldownEndTick(byte spellId)` and `SetCooldownEndTick(byte spellId, int tick)` accept
   any valid 1-based spell ID and correctly index the array without a switch statement.
3. An out-of-range spell ID (e.g. `id = 0` or `id > capacity`) returns `0` / is a no-op,
   with a logged warning.
4. All existing EditMode tick-math tests (`NetworkCombatSecsToTicksTests.cs`,
   `CombatValidatorPureTests.cs`) remain green.
5. All existing PlayMode combat smoke tests (`NetworkCombatProjectileTravelSmokeTests`,
   `CombatFeedbackSmokeTests`) remain green.
6. No spell tuning constants (`Damage`, `Range`, `CastTimeSec`, `CooldownSec`) are changed.
7. No new public API surface beyond what is needed for the refactor.
8. The diff is limited to `NetworkCombatController*.cs` and any directly affected test files.
