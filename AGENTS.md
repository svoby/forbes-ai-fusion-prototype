# Agent Operating Guide (Unity + Fusion)

This repository is a Unity multiplayer prototype using Photon Fusion.

## Main Goal
- Deliver a small playable vertical slice quickly.
- Keep architecture simple, testable, and authority-correct.

## Milestone 1 Scope
- Host/client room connect
- Player spawn
- Movement + look rotation
- Target selection (`Tab`)
- One instant spell (`1`)
- HP sync, death, respawn
- Minimal HUD

## Technical Rules
- Host/State Authority owns gameplay outcomes.
- Clients send input/intent, not final combat results.
- Core simulation runs in Fusion tick callbacks (`FixedUpdateNetwork`).
- Avoid custom physics engine and custom transport in Milestone 1.

## Workflow
1. Implement smallest working version first.
2. Validate with 2 clients.
3. Add one feature at a time.
4. Keep scripts focused and short.

## Definition of Done (per task)
- Works host + 1 client.
- No authority violations.
- No critical console errors.
- Includes quick manual verification steps.

## CLI verification (agents)
When diagnosing compile/regression bugs, **run EditMode tests from the terminal** instead of guessing:

```text
powershell -ExecutionPolicy Bypass -File tools\run-editmode-tests.ps1
```

(`tools\Run-EditModeTests.ps1` delegates to the same runner.)

- Output: `TestResults/editmode.xml` and `TestResults/unity-editmode.log` (see `docs/TEST_HARNESS.md`).
- **Close Unity Editor first** -- batch Unity exits with error if another instance has the project open.
