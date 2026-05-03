# Combat / Projectile System — Cleanup & Stabilization Audit

**Date:** 2026-05-03
**Scope:** Read-only inspection. No production code, tests, or prefabs were changed.
**Branch at audit time:** `checkpoint/combat-travel-audit`

---

## Executive Summary

### Healthy

- The four-phase flow (cast start → cast resolve → projectile travel → impact) is cleanly separated in networked state, authority-correct, and the tick ordering in `FixedUpdateNetwork` is correct.
- `SpellTravelLogic` is a pure static class with zero Fusion dependency; edge cases (zero speed, negative distance, zero tick rate, ceiling rounding) are guarded and tested.
- `CombatValidator` has both the runner-aware entry point and the pure seam-1 overload; the rejection order is locked by 14 EditMode tests.
- `PlayerCombat` is **not attached to `PlayerCharacter.prefab`** — the critical double-damage risk documented in `TEST_COVERAGE_PLAN.md` item 5 is already resolved.
- Death correctly clears both cast state **and** pending impact (`TryCancelCast(Death)` → `ClearCastState` + `ClearPendingImpact`).
- GCD and cooldown timing match WoW semantics (GCD at cast request; cooldown starts at cast start, includes cast time; neither is delayed by projectile travel).
- `SpellTravelLogicTests.cs` contains the correct assertion (`124`, not `100`) — the wrong-assertion bug documented in `docs/combat-travel-checkpoint.md` §4-A has been fixed in the file. The file is **untracked** and just needs to be committed.

### Risky / Needs Attention

- `SpellTravelLogicTests.cs` is **untracked** (`??` in `git status`). A green suite cannot be verified until it is committed and run.
- `PlayerCombat.cs` still exists, still compiles, still responds to `Spell1` in `FixedUpdateNetwork`, and owns a `[Networked] TargetId` property. It is not on the prefab so no gameplay damage occurs today, but it is live dead code.
- `Assets/Tests 1/` is an accidental Unity-generated scaffold folder with a placeholder `NewTestScript` and `Tests 1.asmdef` that appear in the Test Runner alongside real fixtures.
- **One-slot pending impact model** (`PendingImpactSpellId / Target / Tick`) is safe for the current spell table but has an undocumented overwrite hazard for any future instant projectile spell.
- `TargetingController` has **13 unconditional `Debug.Log` calls** not gated by `FORBES_LOG`. Every Tab press emits 4–6 logs; every LMB click emits 3–4.
- `TEST_COVERAGE_PLAN.md` Section A and Risk-map item 5 are stale.

---

## Current Projectile Flow

```text
KEY:  [Networked] = replicated to clients  |  SA = state authority only
```

**Phase 1 — Cast initiation (`TryStartCast`):**
On the SA tick the player presses a spell key, `CombatValidator` runs the rejection chain
(AlreadyCasting → GcdActive → OnCooldown → NoTarget → TargetDead → OutOfRange).
On success, GCD is armed immediately.

- *Instant hitscan* (Arcane Shot / Heavy Blast when cast-time = 0, speed = 0): `DealDamageRpc` fires immediately.
- *Instant projectile* (speed > 0, castTime = 0, currently no spell uses this path): `SchedulePendingImpact` called; damage deferred.
- *Cast-time spell* (castTime > 0): networked cast fields set; cooldown starts at this tick; no damage yet.

**Phase 2 — Mid-cast re-validation (every tick while `IsCasting`):**
If the player moves or jumps, the cast is cancelled (`ClearCastState`; no GCD refund; cooldown already locked in).
Each tick the target is re-validated (existence + liveness + range, ignoring GCD/cooldown) — if invalid, `CancelCast(InvalidTarget)`.

**Phase 3 — Cast resolution (`ResolveCast`):**
Fires the tick `Runner.Tick >= CastEndTick`. Target is re-validated one final time.

- *Cast-time hitscan* (Heavy Blast, speed = 0): `DealDamageRpc` fires here.
- *Cast-time projectile* (Fireball, speed = 20 m/s): `SchedulePendingImpact` called; cast bar closes; damage deferred.

**Phase 4 — Pending impact resolution (`TryResolvePendingImpact`):**
Called at the very top of every `FixedUpdateNetwork` (before cast input is processed).
When `Tick >= PendingImpactTick`:

1. Resolves target by `NetworkId` (tracks by ID, not position — range is not re-checked at impact).
2. If target is gone → `SetFailReason(NoTarget)`, silent miss.
3. If target is dead → `SetFailReason(TargetDead)`, no damage.
4. Otherwise → `DealDamageRpc(spell.Damage)`.

`PendingImpactSpellId` is zeroed before any branch so no double-fire on re-entry.

**Death interruption:**
`FixedUpdateNetwork` checks `_health.IsDead` before anything else. If dead: `ClearCastState` + `ClearPendingImpact`. Both the ongoing cast and any in-flight projectile are abandoned.

**Cooldown / GCD independence:**
Projectile travel time does not extend GCD or spell cooldowns. A Fireball projectile can be in flight while the caster is already off GCD and starting the next cast.

### Spell table (current)

| Spell | castTimeSec | ProjectileSpeed | Path |
| ----- | ----------- | --------------- | ---- |
| Fireball (1) | 1.5 s | 20 m/s | `TryStartCast` → cast state → `ResolveCast` → `SchedulePendingImpact` |
| Arcane Shot (2) | 0 | 0 (hitscan) | `TryStartCast` → `DealDamageRpc` immediately |
| Heavy Blast (3) | 2.5 s | 0 (hitscan) | `TryStartCast` → cast state → `ResolveCast` → `DealDamageRpc` |

---

## Findings

### Finding 1 — `SpellTravelLogicTests.cs` is untracked

**File:** `Assets/Tests/EditMode/SpellTravelLogicTests.cs`
**Risk:** Medium — the EditMode suite cannot be treated as a verified green baseline until this file is committed and run. The file content is correct (both assertions pass against the implementation).

**Fix:**

```powershell
git add Assets/Tests/EditMode/SpellTravelLogicTests.cs
git commit -m "test: commit SpellTravelLogicTests (correct ImpactTick assertion)"
```

Then run `Forbes.Tests.EditMode` in the Editor Test Runner and confirm all green.

**Behavior change:** No.
**Tests required before touching:** None — just commit and run.

---

### Finding 2 — `Assets/Tests 1/` accidental scaffold folder

**Files:** `Assets/Tests 1/NewTestScript.cs`, `Assets/Tests 1/Tests 1.asmdef` (and their `.meta` files)
**Risk:** Low-medium — two empty passing tests pollute the Test Runner count; the `Tests1` assembly has no game-code references so it does not affect production compilation or behavior.

**Fix:** Delete the entire `Assets/Tests 1/` folder and its `.meta` file. Verify that the test count in `editmode.xml` does not change for `Forbes.Tests.EditMode`.

**Behavior change:** No.
**Tests required before touching:** Run `Forbes.Tests.EditMode` before and after deletion; confirm fixture count is unchanged.

---

### Finding 3 — `PlayerCombat.cs` is live dead code

**File:** `Assets/Scripts/Player/PlayerCombat.cs`
**Risk:** Medium — not on the prefab (confirmed: grep of `PlayerCharacter.prefab` returns no match), so no double-damage occurs today. However the script:

- Compiles into `Forbes.Runtime`.
- Contains `FixedUpdateNetwork` that reads `GameplayInput.Spell1` and calls `DealDamageRpc` with its own `SpellDamage = 15f`, bypassing `CombatValidator`.
- Carries a `[Networked] public NetworkId TargetId` property.

If accidentally re-added to the prefab via "Add Component", it would silently deal a second 15-damage hit on every Spell1 press.
The class comment says "LEGACY — will be replaced by NetworkCombatController in Milestone 3." That milestone has effectively landed.

**Fix:** Delete `Assets/Scripts/Player/PlayerCombat.cs` and its `.meta` file. Update `TEST_COVERAGE_PLAN.md` risk-map item 5 to mark it resolved (see Finding 9).

**Behavior change:** No.
**Tests required before touching:** Confirm `PlayerCharacter.prefab` has no `PlayerCombat` component (already confirmed). Run EditMode tests after deletion.

---

### Finding 4 — One-slot pending impact: undocumented overwrite hazard

**File:** `Assets/Scripts/Combat/NetworkCombatController.cs` (lines ~54–61, ~279–322)
**Risk:** Medium (future), Low (current).

**Current safety:** Only Fireball has `ProjectileSpeedMetersPerSecond > 0` and it is cast-time. `SchedulePendingImpact` is called only in `ResolveCast`, never in `TryStartCast` for cast-time spells. The first Fireball's impact tick always arrives before a second Fireball's `ResolveCast` can overwrite it.

**The dangerous path that does not exist yet:** Any spell with `castTimeSec = 0` AND `ProjectileSpeedMetersPerSecond > 0` (instant projectile) would call `SchedulePendingImpact` inside `TryStartCast`. Two consecutive instant-projectile casts in adjacent ticks would silently drop the first impact.

**Fix (comment + observable log — no behavior change):**

```csharp
// ONE-SLOT MODEL: only one pending impact at a time. Safe with the current
// spell table because the only projectile spell (Fireball) is cast-time and
// its impact fires before a second cast could complete. If a second instant
// projectile spell is ever added, upgrade to a small queue (NetworkLinkedList).
void SchedulePendingImpact(byte spellId, NetworkId targetId, int impactTick) {
    if (PendingImpactSpellId != 0) {
        ForbesLog.Net($"SchedulePendingImpact: overwriting pending impact spellId={PendingImpactSpellId} — one-slot limit.", this);
    }
    ...
```

**Behavior change:** No (comment + conditional log only).
**Tests required:** None for the comment. For the log guard: add an EditMode test if a second projectile spell is ever introduced.

---

### Finding 5 — Pending impact validates only existence + liveness, not range or LoS

**File:** `Assets/Scripts/Combat/NetworkCombatController.cs` (`TryResolvePendingImpact`)
**Risk:** Low — this is the intended design.

At impact time, the code checks only: (1) target still resolvable by `NetworkId`, (2) `Health` component present, (3) `IsDead == false`. Range and line-of-sight are not re-checked. This is proven by the smoke test `ProjectileSpell_TargetMovesOutOfCastRange_StillDamagedByNetworkId`.

**Fix (comment only):**

```csharp
// Impact validates only existence and liveness — not range or LoS.
// Once scheduled, a projectile tracks the target by NetworkId only.
// Covered by smoke: ProjectileSpell_TargetMovesOutOfCastRange_StillDamagedByNetworkId.
```

**Behavior change:** No.

---

### Finding 6 — Caster death clears pending impact: correct but untested

**File:** `Assets/Scripts/Combat/NetworkCombatController.cs` (`TryCancelCast`, lines ~150–157)
**Risk:** Low — the logic is correct. `TryCancelCast(Death)` calls `ClearCastState()` then `ClearPendingImpact()`. If the caster dies while a Fireball is in flight the projectile is abandoned silently; the target is never damaged. This is a deliberate policy choice.

No PlayMode smoke test covers this scenario. The four existing `NetworkCombatProjectileTravelSmokeTests` do not include caster death.

**Fix:** Add a fifth smoke test (see Tests to Add First below).

**Behavior change:** No.

---

### Finding 7 — Cast cancellation does not clear pending impact: correct but undocumented

**File:** `Assets/Scripts/Combat/NetworkCombatController.cs` (`TryCancelCast`, lines ~160–165)
**Risk:** Low — for `Movement / Jump / NewSpell / InvalidTarget` reasons, `TryCancelCast` calls only `ClearCastState()`. This is correct: pending impacts only exist after `ResolveCast` fires; cancellation fires before cast completion so there is nothing to clear. Movement cannot cancel an already-launched projectile.

This is non-obvious from reading the two `Clear*` methods side-by-side.

**Fix (comment only):**

```csharp
// Non-Death cancellations clear cast state only, not pending impact:
// cancellation fires before ResolveCast so no pending impact has been
// scheduled yet. Death clears both because it can interrupt mid-flight.
```

**Behavior change:** No.

---

### Finding 8 — `TargetingController` logging noise (13 unconditional `Debug.Log` calls)

**File:** `Assets/Scripts/Combat/TargetingController.cs`
**Risk:** Medium — every Tab press emits ~5 `Debug.Log` lines; every LMB click emits ~4. These are raw `Debug.Log`, **not** gated by `[Conditional("FORBES_LOG")]` like `ForbesLog.Net`/`ForbesLog.Health`. They always fire regardless of the `FORBES_LOG` scripting define.

Unaffected lines (keep as-is):

- Startup "Running on…" `Debug.Log` (fires once, useful for configuration diagnosis).
- All `Debug.LogWarning` calls (null camera, null mouse, no candidates — real error signals).

Affected lines (should be gated):

- All verbose per-event `Debug.Log(...)` in `CycleTarget` and `TrySelectFromScreenRay`.
- `SetTarget` "Target → …" line.

**Fix:** Add a `Targeting` channel to `ForbesLog.cs`:

```csharp
[Conditional("FORBES_LOG")]
public static void Targeting(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesTargeting] " + message, context);
}
```

Replace the 10 verbose `Debug.Log(...)` calls in `CycleTarget`, `TrySelectFromScreenRay`, and `SetTarget` with `ForbesLog.Targeting(...)`. Run PlayMode targeting tests to confirm no behavior change.

**Behavior change:** No (logs are not gameplay-relevant).
**Tests required:** Run `Forbes.Tests.PlayMode` targeting fixtures after the change.

---

### Finding 9 — `TEST_COVERAGE_PLAN.md` has two stale statements

**File:** `docs/TEST_COVERAGE_PLAN.md`
**Risk:** Low — documentation only, but misleads any agent reading the plan as the current architecture snapshot.

1. **Section A, paragraph 1:** Claims "No `Assets/Tests/` folder yet, no asmdefs under `Assets/Scripts/`." Both `Forbes.Tests.EditMode`, `Forbes.Tests.PlayMode`, and `Assets/Scripts/Forbes.Runtime.asmdef` now exist.
2. **Risk-map item 5:** Claims "legacy `PlayerCombat` still on prefab … can re-fire damage on `Spell1`." `PlayerCombat` is no longer on the prefab.

**Fix:** Update `docs/TEST_COVERAGE_PLAN.md`:

- Section A paragraph 1: note the plan was originally drafted before the asmdefs and test folders existed; both now exist.
- Risk-map item 5: add "Status: resolved — `PlayerCombat` removed from prefab (see `docs/CODE_CLEANUP_AUDIT.md` Finding 3)."

**Behavior change:** No.

---

### Finding 10 — Test-only internal hooks in production code

**Files:**

- `Assets/Scripts/Player/Health.cs` line 92: `internal void AuthorityResetNetworkedHealthToStartingForTests()`
- `Assets/Scripts/Combat/TargetingController.cs` line 40: `internal static bool SuppressLocalSelectionInputInTests { get; set; }`

**Risk:** Low. Both are `internal` (invisible outside `Forbes.Runtime`). `SuppressLocalSelectionInputInTests` adds one bool branch to `Update()` per frame (negligible). The `"ForTests"` name suffix is an honest label. Per-project audit rules these must be flagged as production code added mainly for tests.

**Fix:** No immediate action required. If `Health` grows: evaluate whether `AuthorityResetNetworkedHealthToStartingForTests` can be removed once `AuthorityApplyStartingHealthIfUnset` suffices for all smoke scenarios.

---

### Finding 11 — `TickRateRounded` vs float `Runner.TickRate` inconsistency

**File:** `Assets/Scripts/Combat/NetworkCombatController.cs` (line ~277)
**Risk:** Very low. Travel-tick computation passes `Mathf.RoundToInt(Runner.TickRate)` to `SpellTravelLogic.ComputeTravelTicks` (which takes `int`), while `SecsToTicks` uses `Runner.TickRate` as a float. At Fusion's standard 60 Hz both resolve identically. Could diverge at non-integer tick rates.

**Fix (comment only):**

```csharp
// SpellTravelLogic takes int tickRate; RoundToInt matches the precision
// used by SecsToTicks (CeilToInt on float). At 60 Hz both are equal.
int TickRateRounded => Mathf.RoundToInt(Runner.TickRate);
```

**Behavior change:** No.

---

## Findings Summary

| # | File / Area | Risk | Action |
| - | ----------- | ---- | ------ |
| 1 | `SpellTravelLogicTests.cs` untracked | Medium | `git add` + commit; run suite |
| 2 | `Assets/Tests 1/` scaffold folder | Low-Medium | Delete folder + metas |
| 3 | `PlayerCombat.cs` dead code | Medium | Delete script + meta |
| 4 | One-slot pending impact (undocumented) | Medium (future) | Add comment + overwrite log |
| 5 | Pending impact no-range policy (undocumented) | Low | Add comment |
| 6 | Caster death clears pending impact (untested) | Low | Add PlayMode smoke test |
| 7 | Cast cancellation ≠ clear pending (undocumented) | Low | Add comment |
| 8 | `TargetingController` logging noise | Medium | Gate verbose logs behind `FORBES_LOG` |
| 9 | `TEST_COVERAGE_PLAN.md` stale | Low | Update two passages |
| 10 | Test-only `internal` hooks | Low | Track; no immediate change |
| 11 | `TickRateRounded` inconsistency | Very Low | Add comment |

---

## Proposed Commit Sequence

All commits are non-breaking and independently reviewable. None change gameplay behavior.

**Commit 1 — `fix/commit-spell-travel-tests`**

```bash
git add Assets/Tests/EditMode/SpellTravelLogicTests.cs
```

Run `Forbes.Tests.EditMode` in Test Runner. Confirm all green. Gate for all subsequent commits.

**Commit 2 — `chore/delete-tests-1-scaffold`**
Delete `Assets/Tests 1/` folder and all files inside it including `.meta` files.
Run EditMode suite; confirm fixture count unchanged.

**Commit 3 — `chore/remove-player-combat-dead-code`**
Delete `Assets/Scripts/Player/PlayerCombat.cs` and `Assets/Scripts/Player/PlayerCombat.cs.meta`.
Run EditMode tests. Manual smoke: cast Arcane Shot — confirm single 15-dmg hit only.

**Commit 4 — `docs/stale-test-coverage-plan`**
Update `docs/TEST_COVERAGE_PLAN.md` Section A paragraph 1 and risk-map item 5 (Findings 9).
No code changes.

**Commit 5 — `docs/combat-projectile-policy-comments`**

Add in-code comments only (no logic changes) for Findings 4, 5, 7, 11:

- One-slot overwrite warning above `SchedulePendingImpact`.
- Overwrite observable log inside `SchedulePendingImpact` (guarded by `ForbesLog.Net`).
- Impact validation policy comment inside `TryResolvePendingImpact`.
- Cast-cancellation vs pending-impact comment inside `TryCancelCast`.
- `TickRateRounded` comment.

**Commit 6 — `fix/targeting-controller-log-noise`**
Add `ForbesLog.Targeting` channel to `ForbesLog.cs`.
Migrate 10 verbose `Debug.Log(...)` calls in `CycleTarget`, `TrySelectFromScreenRay`, and `SetTarget` to `ForbesLog.Targeting(...)`.
Keep all `Debug.LogWarning` calls and the startup `Debug.Log("Running on…")` unchanged.
Run PlayMode targeting tests.

**Commit 7 — `test/caster-death-clears-pending-impact`**
Add fifth smoke test to `NetworkCombatProjectileTravelSmokeTests` (see next section).

---

## Tests to Add First

### Priority 1 — Prerequisite (Commit 1)

Run `Forbes.Tests.EditMode` in the Editor and confirm all existing tests pass (including `SpellTravelLogicTests`). This is the gate for every subsequent commit.

### Priority 2 — Caster-death smoke (Commit 7)

Add to `Assets/Tests/PlayMode/NetworkCombatProjectileTravelSmokeTests.cs`:

```csharp
[UnityTest]
[Timeout(120000)]
public IEnumerator ProjectileSpell_CasterDiesBeforeImpact_PendingImpactCleared_TargetUnharmed() {
    // Setup: spawn player + dummy in range, same as other smoke tests.
    // 1. Wait for session + spawns + AuthorityApplyStartingHealthIfUnset.
    // 2. Fire Spell1 (Fireball). Wait until PendingImpactSpellId != 0.
    // 3. Record impactTick and startDummyHp.
    // 4. Kill caster: playerHealth.DealDamageRpc(playerHealth.StartingHealth + 1f).
    // 5. WaitUntil playerHealth.IsDead.
    // 6. WaitUntil runner.Tick > impactTick + 2.
    // Assert: combat.PendingImpactSpellId == 0 (cleared by death).
    // Assert: dummyHealth.NetworkedHealth ~= startDummyHp (no damage applied).
}
```

Use `FusionPlayModeTestHelpers.WaitUntil` and `WaitFrames` consistently with the existing four tests. Do not copy the setup coroutine; share the `Body` / `_session` / `_player` / `_dummy` pattern already in place.

---

## Do Not Touch Yet

The following are working correctly with adequate coverage. Refactoring would be scope creep.

| Area | Why hands-off |
| ---- | ------------- |
| `NetworkCombatController.FixedUpdateNetwork` ordering | dead → pending → cast → input is load-bearing. Reordering risks subtle tick-timing bugs. |
| `SpellTravelLogic` | Clean, pure, fully tested. |
| `CombatValidator` two-overload design | Correct seam-1 already implemented and tested. |
| `SecsToTicks` internal static seam | Correct and covered by `NetworkCombatSecsToTicksTests`. |
| `ResolveCast` re-validation with `gcdEndTick: 0, cooldownEndTick: 0` | Intentional: checks target validity only, not re-cast permission. |
| `SchedulePendingImpact` inline `TryResolvePendingImpact` call | Correct zero-distance / same-tick guard; documented in `combat-travel-checkpoint.md` §4-D. |
| `Health.DealDamageRpc` authority + dead guards | Both required. Do not reorder or remove either guard. |
| GCD timing (at cast request) + cooldown timing (starts at cast start, includes cast duration) | WoW-correct semantics. Do not adjust any constant. |
| `SpellRegistry` hardcoded table | Appropriate for prototype scale. Do not convert to ScriptableObjects unless spell count exceeds ~8. |
| `ForbesLog.Net` / `ForbesLog.Health` `[Conditional("FORBES_LOG")]` pattern | Correct pattern; do not change these two existing channels. |
| PlayMode smoke infrastructure | `FusionSinglePlayerTestSession`, `FusionPlayModeTestHelpers`, `FusionPlayModeTestInputRelay` are centralized and correct. Do not duplicate coroutine helpers. |
| `TargetingController` dead-target auto-clear (`ClearCurrentTargetIfDead`) | Correct automatic housekeeping preventing stale `NetworkId` in input. |
| `PlayerMovement.FixedUpdateNetwork` ordering | dead first, then casting freeze, then input. Do not reorder. |
| `Health.AuthorityApplyStartingHealthIfUnset` | Safe one-shot startup stabilizer for PlayMode tests. Keep as-is. |
| Cooldown slot switch statements (`GetCooldownEndTick` / `SetCooldownEndTick`) | Acceptable prototype shortcut for 3 spells. Convert to `NetworkArray` only when spell count grows. |
