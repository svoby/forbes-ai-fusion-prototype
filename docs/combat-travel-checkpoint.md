# Combat Travel Checkpoint Audit

**Branch:** `checkpoint/combat-travel-audit`  
**Date:** 2026-05-03  
**Scope:** Read-only inspection of the current combat / spellcasting implementation before adding further projectile-delay behaviour. No production code was changed in this branch.

---

## 1. Current Spell Flow

### Full pipeline

```
KeyboardInputSource.Update
  → FusionInputProvider.OnInput (INetworkRunnerCallbacks)
      GameplayInput { Move, LookYaw, TargetId, Buttons{Spell1|2|3, Jump, …} }

NetworkCombatController.FixedUpdateNetwork  ← state authority only
  │
  ├─ if IsDead            → TryCancelCast(Death) + ClearPendingImpact; return
  │
  ├─ TryResolvePendingImpact()
  │     if PendingImpactSpellId != 0 && Tick >= PendingImpactTick
  │       → runner.TryFindObject(PendingImpactTarget)
  │       → if alive: Health.DealDamageRpc(spell.Damage)
  │
  ├─ if IsCasting && Tick >= CastEndTick  → ResolveCast()
  │
  └─ GetInput → Spell1/2/3 pressed
        TryCastOrInterrupt → (cancel ongoing cast if any) → TryStartCast
          CombatValidator.TryValidate
            AlreadyCasting → GcdActive → OnCooldown → NoTarget → TargetDead → OutOfRange

        ┌─ castTimeSec == 0  (instant)
        │     GCD armed; cooldown set
        │     if HasProjectile(spell): SchedulePendingImpact(spellId, targetId, impactTick)
        │     else:                    Health.DealDamageRpc(damage)        ← hitscan
        │
        └─ castTimeSec > 0  (cast-time)
              Set CurrentSpellId / CastTarget / CastStartTick / CastEndTick
              Cooldown begins at cast START (matches WoW: cancellation cannot bypass CD)
              ── player moves or presses Jump → TryCancelCast
              ── per-tick: re-validate target; cancel with InvalidTarget if lost

ResolveCast()  (fires when Tick >= CastEndTick)
  Re-validate (target may have died / left range during cast)
  if HasProjectile(spell): SchedulePendingImpact(spellId, CastTarget, impactTick)
  else:                    Health.DealDamageRpc(damage)
  ClearCastState()
```

### Cast time vs travel time — explicitly distinct

| Phase | Networked fields | Active while |
|---|---|---|
| Cast time | `CurrentSpellId`, `CastStartTick`, `CastEndTick` | `CurrentSpellId != 0 && Tick < CastEndTick` |
| Travel time | `PendingImpactSpellId`, `PendingImpactTarget`, `PendingImpactTick` | `PendingImpactSpellId != 0 && Tick < PendingImpactTick` |

The two phases are fully decoupled. Movement cancels the cast phase; once a pending impact is scheduled it is not cancellable by movement (it can only be voided by the target dying before `PendingImpactTick`).

---

## 2. Current Travel-Time State

### `ProjectileSpeedMetersPerSecond` in `SpellData`

Defined as a field in `SpellData` (inside `Assets/Scripts/Combat/SpellRegistry.cs`).

| Spell | Speed | Path |
|---|---|---|
| Fireball (id 1) | 20 m/s | cast-time → ResolveCast → SchedulePendingImpact |
| Arcane Shot (id 2) | 0 (hitscan) | instant → DealDamageRpc |
| Heavy Blast (id 3) | 0 (hitscan) | cast-time → ResolveCast → DealDamageRpc |

Convention: `speed <= 0` means hitscan; `speed > 0` means logical projectile.

### `SpellTravelLogic` exists

`Assets/Scripts/Combat/SpellTravelLogic.cs` — pure static class, no Fusion dependency:

```csharp
HasProjectile(spell)                          // speed > 0
ComputeTravelTicks(dist, speed, tickRate)     // ceil(dist / speed * rate), guards ≤ 0
ComputeImpactTick(releaseTick, travelTicks)   // releaseTick + max(0, travelTicks)
```

### Damage is delayed after cast completion for projectile spells

`TryStartCast` / `ResolveCast` call `SchedulePendingImpact`, which writes three `[Networked]` properties:

```csharp
PendingImpactSpellId   // byte  — 0 = idle
PendingImpactTarget    // NetworkId
PendingImpactTick      // int
```

`TryResolvePendingImpact` runs at the top of every `FixedUpdateNetwork` tick. When `Tick >= PendingImpactTick`, it resolves the target by `NetworkId`, checks liveness, and calls `Health.DealDamageRpc`. Damage is **not** applied at `ResolveCast`; it is applied at `PendingImpactTick`.

`SchedulePendingImpact` additionally calls `TryResolvePendingImpact` inline if the impact tick is already reached (zero-distance / same-tick edge case).

### Travel-time math is covered by tests

- **EditMode:** `SpellTravelLogicTests` — 6 unit tests on `ComputeTravelTicks` and `ComputeImpactTick` guards (see risk item A below for a wrong assertion).  
- **PlayMode:** `NetworkCombatProjectileTravelSmokeTests` — 4 Fusion `GameMode.Single` scenarios:
  1. Damage not applied before impact tick.
  2. Damage applied after impact tick.
  3. Dead target: no delayed damage, pending cleared safely.
  4. Target teleported out of cast range after scheduling: still damaged (by `NetworkId` lock, not position).

---

## 3. Current Tests

### EditMode fixtures (`Forbes.Tests.EditMode`)

All 10 fixtures are pure logic with no `NetworkRunner` dependency.

| Fixture | Tests | What it pins |
|---|---|---|
| `CombatFailReasonEnumTests` | 8 | Enum-to-byte mapping (wire-stable) |
| `CombatValidatorPureTests` | 14 | Rejection order via pre-resolved Transform seam |
| `HealthDefaultsTests` | 6 | `StartingHealth=100`, `RespawnDelaySeconds=3`, networked property reflection |
| `SpellRegistryTests` | 9 | All spell fields + `Get` bounds |
| `NetworkCombatSecsToTicksTests` | 7 | `SecsToTicks(tickRate, seconds)` via `internal static` seam |
| `SpellTravelLogicTests` | 6 | `ComputeTravelTicks` / `ComputeImpactTick` guards (**see risk A**) |
| `NetworkMobBrainLogicTests` | — | Mob AI state machine (pure) |
| `CastBarLayoutDefaultsTests` | — | UI layout defaults |
| `CastBarHudRegressionTests` | — | Cast bar render regressions |
| `TargetHealthBarLogicTests` | — | Target HP bar math |

### PlayMode fixtures (`Forbes.Tests.PlayMode`)

Require a Fusion runner (`GameMode.Single`); cannot run in EditMode batch.

- `NetworkCombatProjectileTravelSmokeTests` — 4 tests (see section 2).
- `FusionHealthSmokeTests` (and others) — pre-existing Fusion smokes.

### CLI test run result — **stale; not a fresh baseline**

```
powershell -ExecutionPolicy Bypass -File .\tools\run-editmode-tests.ps1
```

Exit code: **0**; script reported "OK". However the XML (`TestResults/editmode.xml`) is **stale**:

- Assembly path in XML: `C:/unity/forbes-test-mirror/Library/ScriptAssemblies/Forbes.Tests.EditMode.dll`  
  (different project path — the Library cache was not recompiled in this batchmode run)
- XML timestamp: `2026-05-03 08:28:15Z` — the run was at `2026-05-03 19:01:35Z` (10.5 hours later)
- XML contains only 31 tests across 4 fixtures (`CombatFailReasonEnumTests`, `CombatValidatorPureTests`, `HealthDefaultsTests`, `SpellRegistryTests`); `SpellTravelLogicTests` and 6 other fixtures are absent

The Unity log confirms batchmode did asset refresh and exited cleanly, but no test output is present in the log — the test runner used the pre-existing DLL rather than a recompiled one. **These 31 tests are from an older version of the assembly; they are not a valid baseline for the current code.**

The Unity Editor is likely open on this project, preventing batchmode from recompiling.

### How to get a verified green run

With the Unity Editor open, use the Test Runner window:

```
Window → General → Test Runner → Edit Mode tab
→ select assembly Forbes.Tests.EditMode
→ Run All
```

Results also write to `TestResults/last-editor-test-run.log`.

---

## 4. Risk Assessment

### A. Wrong assertion in `SpellTravelLogicTests` — **Weakened test**

`Assets/Tests/EditMode/SpellTravelLogicTests.cs`, line 35:

```csharp
// Method: ComputeImpactTick_IsReleasePlusTravel_And_TravelFloorsAtZero
Assert.AreEqual(100, SpellTravelLogic.ComputeImpactTick(100, 24));  // WRONG — expects 100, actual is 124
Assert.AreEqual(100, SpellTravelLogic.ComputeImpactTick(100, -3));  // Correct — negative floors to 0
```

`ComputeImpactTick(releaseTick: 100, travelTicks: 24)` returns `100 + max(0, 24) = 124`.  
The expected value on line 35 must be `124`, not `100`.

The second assertion (`travelTicks: -3` → `100 + 0 = 100`) is correct.

This test has **never been executed by the CI or the Editor test runner** — it was added in commit `5cb077e` after the last recorded editor run (`2026-05-03 08:28:15Z`). It would fail if run. This is the only critical blocker.

**Classification: weakened test (wrong assertion). Must be fixed before further feature work.**

### B. Stale Library cache in batchmode — **Symptom of environment state**

The batchmode runner reused a pre-existing `Forbes.Tests.EditMode.dll` compiled for path `C:/unity/forbes-test-mirror/` instead of recompiling for the current project. This means the CLI exit code cannot be trusted as a green signal when the Editor is open.

**Classification: CI environment issue. Not a code smell; documented here for awareness.**

### C. `GetCooldownEndTick` / `SetCooldownEndTick` switch-based 3-slot storage — **Acceptable prototype shortcut**

```csharp
int GetCooldownEndTick(byte spellId) => spellId switch { 1 => Cooldown1EndTick, … _ => 0 };
```

Correct and readable for 3 spells. Would require a `NetworkArray` or dictionary if spells grew past ~5.

**Classification: acceptable prototype shortcut. No action required.**

### D. `SchedulePendingImpact` calls `TryResolvePendingImpact` inline — **Legitimate Fusion timing guard**

```csharp
void SchedulePendingImpact(…) {
    PendingImpactSpellId = spellId; PendingImpactTarget = targetId; PendingImpactTick = impactTick;
    if (Runner.Tick >= impactTick) { TryResolvePendingImpact(); }  // zero-distance / same-tick
}
```

Prevents a one-tick delay on zero-distance casts. `TryResolvePendingImpact` clears `PendingImpactSpellId` first, so a second call on the next tick is a no-op (no double damage).

**Classification: legitimate Fusion timing guard.**

### E. `TryResolvePendingImpact` fires at the top of every `FixedUpdateNetwork` — **Legitimate authority guard**

Ensures impact resolution runs every simulation tick regardless of cast or input state.

**Classification: legitimate authority design.**

### F. In-cast target re-validation bypasses GCD/cooldown — **Legitimate authority guard**

```csharp
CombatValidator.TryValidate(…, gcdEndTick: 0, cooldownEndTick: 0, isAlreadyCasting: false, …)
```

Correct: during an ongoing cast we only care whether the target is still valid, not whether a new cast would be allowed.

**Classification: legitimate authority guard.**

### G. `TEST_COVERAGE_PLAN.md` section A is stale — **Documentation drift**

Section A describes the architecture before `Forbes.Runtime.asmdef` was added and before PlayMode tests were written. Both now exist. The risk tables and test specs remain accurate.

**Classification: documentation drift, not a code risk. Low priority to update.**

---

## 5. Recommendation

**C — Fix the wrong test assertion and verify a clean run before adding more projectile behaviour.**

### Rationale

The logical travel-time implementation (commit `5cb077e`) is complete, well-structured, and authority-correct:

- `SpellData.ProjectileSpeedMetersPerSecond` correctly distinguishes hitscan from projectile spells.
- `SpellTravelLogic` is a pure static class with no Fusion dependency — easy to test and reason about.
- `PendingImpactSpellId/Target/Tick` decouple travel time cleanly from cast time at the networked state level.
- Dead-target guard on impact is present and correct.
- PlayMode smokes cover the main scenarios.

The only blocker is a single wrong expected value in one EditMode test (risk A). Adding more projectile behaviour on top of an unverified test suite means a future regression could be masked by the already-broken assertion.

### Minimum required action before next feature work

1. Fix `SpellTravelLogicTests.cs` line 35:  
   `Assert.AreEqual(124, SpellTravelLogic.ComputeImpactTick(100, 24));`

2. Run EditMode tests in the Editor's Test Runner and confirm all pass:  
   `Window → General → Test Runner → Edit Mode → Forbes.Tests.EditMode → Run All`

3. (Optional but recommended) Update `docs/TEST_COVERAGE_PLAN.md` section A to reflect the current asmdef and PlayMode test presence.

Once the suite is verified green, it is safe to add the next layer of projectile behaviour (e.g., visual projectile prefab, travel cancellation on caster death, etc.) without risk of silent regressions.
