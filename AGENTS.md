# Agent Operating Guide (Unity + Fusion)

This repository is a Unity multiplayer prototype using Photon Fusion.

## Main goal
- Deliver a small playable vertical slice quickly.
- Architecture: simple, testable, **authority-correct**.

## Current prototype context
- **Networking:** host/client room, networked player **spawn**, **movement + look** (third-person).
- **Combat / state:** **Health**, **death**, **respawn**; spell damage via networked combat path; **Tab** target selection.
- **UI:** minimal HUD (player HP, cast bar, selected-target info / world target HP bar).
- **Mobs:** **`TrainingDummy`** prefab includes **`NetworkMobBrain`** — **wander**, **facing**, **melee**, **aggro**, **chase**, **leash/return** (prototype AI, state authority on mob).
- **Tests:** **EditMode** — pure logic (validators, `NetworkMobBrainLogic`, bar math, defaults). **PlayMode** — Fusion **`GameMode.Single`** smoke (`FusionSinglePlayerTestSession`); reuse **`FusionPlayModeTestHelpers`** for runner lifecycle, **`WaitUntil`/`WaitFrames`**, spawn polling, teleport, and **`PinMobBrainNoCombat`** (do not copy-paste coroutine helpers).

## Technical rules
- **State Authority** owns gameplay outcomes; clients send **intent/input**, not authoritative combat results.
- Core simulation in Fusion ticks (`FixedUpdateNetwork`), not in render-only `Update`/`LateUpdate`.
- Milestone 1 avoids custom transport and a custom physics engine.

## Workflow
1. Smallest working change first.
2. Validate with host + one client when behavior is networked.
3. One feature at a time; keep behaviours focused.
4. Do **not** merge feature branches into `main`/`master` from the agent (no automatic merge); the user opens a PR and merges when ready (see `.cursor/rules/feature-branching.mdc`).

## Definition of done (per task)
- Works **host + one client** where relevant.
- No **authority violations** (see mandatory audit flags).
- No critical console errors.
- Short manual verification notes.

## Mandatory post-implementation diff audit

**When:** After **any** feature that needed **multiple fix iterations** (flaky tests, regressions, trial-and-error) before landing.

**Before claiming the task complete, stop and audit the final diff.** Green tests are **not** enough; the diff must also be **intentional and minimal**.

**The audit must:**
1. Map the diff to the original request; call out scope drift.
2. Classify every non-trivial change as **required**, **cleanup**, **suspicious**, or **unrelated** (unrelated → revert or split to another change).
3. **Not** implement new product behavior during the audit — classify, flag, propose the smallest safe follow-up only.

**The audit must explicitly flag:**
- **Weakened tests** (looser assertions, dropped cases, mismatched intent).
- **Production code added mainly for tests** (broad `InternalsVisibleTo`, public hooks only fixtures use, behaviour toggles that exist for tests).
- **Changed serialized defaults** (prefab fields, inspector tuning) without product justification.
- **Public API expansion** when `internal` or test-local seams would suffice.
- **Duplicate test helpers** that should live in one place.
- **Prefab / scene / `.meta` churn** that looks accidental or unrelated.
- **Authority violations** (gameplay outcomes or authoritative state off state authority; trusting client-only paths for combat).
- **`FindObjectsByType` / scene-wide scans in hot gameplay paths** (tick-hot cost and hidden coupling).

Full checklist: `.cursor/rules/post-feature-diff-audit.mdc`.

## CLI verification (agents)
For compile/regression work, run **EditMode** tests from the shell (Editor must be **closed** for this project’s batch Unity):

```text
powershell -ExecutionPolicy Bypass -File tools\run-editmode-tests.ps1
```

(`tools\Run-EditModeTests.ps1` delegates to the same runner.)

- Results: `TestResults/editmode.xml`, log: `TestResults/unity-editmode.log` (see `docs/TEST_HARNESS.md`).
- **PlayMode:** `tools\run-playmode-tests.ps1` — same **close Editor** constraint; or run **Play Mode** in **Test Runner** with the Editor open.
