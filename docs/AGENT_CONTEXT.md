# Agent Context Guide

This file tells AI coding agents (Cursor, Composer, CLI agents) which documents are
current sources of truth and how to pick the smallest useful context bundle for a task.

---

## Current source-of-truth documents

| Document | When to read |
|---|---|
| `docs/PROJECTILE_POLICY.md` | Combat, spells, targeted missiles, projectile visuals, missile manager discussions |
| `docs/TEST_HARNESS.md` | Running EditMode/PlayMode tests; understanding test commands and results |
| `docs/TEST_COVERAGE_PLAN.md` | Planning test coverage or refactor safety; not needed for every small change |
| `docs/architecture.md` | Broad system orientation; prefer more specific docs for concrete coding tasks |

---

## Common context bundles

### Combat / missiles
- `docs/PROJECTILE_POLICY.md`
- `docs/TEST_HARNESS.md`
- `Assets/Scripts/Combat/NetworkCombatController.cs`
- `Assets/Scripts/Combat/SpellTravelLogic.cs`
- `Assets/Scripts/Combat/SpellRegistry.cs`
- `Assets/Scripts/Combat/CombatValidator.cs`
- `Assets/Scripts/Player/Health.cs`
- `Assets/Tests/EditMode/SpellTravelLogicTests.cs`
- `Assets/Tests/PlayMode/NetworkCombatProjectileTravelSmokeTests.cs`

### Input / movement / camera
- `docs/TEST_HARNESS.md`
- `docs/architecture.md`
- `Assets/Scripts/Networking/FusionInputProvider.cs`
- `Assets/Scripts/Player/KeyboardInputSource.cs`
- `Assets/Scripts/Player/PlayerMovement.cs`
- `Assets/Scripts/Player/ThirdPersonOrbitCamera.cs`
- Relevant EditMode/PlayMode tests

### UI / HUD / cast bar
- `docs/TEST_HARNESS.md`
- `Assets/Scripts/UI/`
- `Assets/Tests/EditMode/` — CastBar* tests
- Relevant PlayMode smoke tests if UI is runtime-dependent

### Testing / harness
- `docs/TEST_HARNESS.md`
- `docs/TEST_COVERAGE_PLAN.md`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/`
- `tools/` (test runner scripts)

---

## Current projectile / missile summary

- Normal targeted spell projectiles are **authoritative targeted missiles**.
- **State Authority** owns missile simulation and damage application.
- The missile follows a moving target each simulation tick.
- Cosmetic projectile visuals are future/local-only and must **never apply damage**.
- No Counter-Strike-style hitscan bullets.
- No `Runner.Spawn` NetworkObject projectiles for normal targeted spells.
- No object pooling yet.
- No global `TargetedMissileManager` yet; current storage is prototype-level inside `NetworkCombatController`.

---

## Agent rules

- **Prefer small, scoped edits.** Do not scan the whole repository unless doing an explicit audit.
- **Read the smallest relevant context bundle first** before writing code.
- **Do not treat removed or historical docs as current guidance.** If an old doc is absent, do not recreate it unless explicitly asked.
- **Do not change gameplay constants** (tuning values, serialized defaults) unless explicitly requested.
- **Do not combine refactor + feature + visual polish in one task.**
- **For behavior changes, add or update tests.**
- **For pure logic, prefer EditMode tests.** For Fusion runtime behavior, use PlayMode smoke tests.
- **Keep `NetworkCombatController` as Fusion orchestration** unless a specific refactor task says otherwise.
- **Extract pure logic** out of `NetworkCombatController` when it is testable and not Fusion-specific.

---

## Removed / historical docs

Older cleanup and checkpoint documents may have existed in `docs/` historically. They should
**not** be recreated or used as current guidance unless explicitly requested by the user.
