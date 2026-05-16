# Agent Context Guide

This file tells AI coding agents (Cursor, Composer, CLI agents) which documents are
current sources of truth and how to pick the smallest useful context bundle for a task.

---

## Current source-of-truth documents

| Document | When to read |
|---|---|
| `docs/architecture.md` | Broad system orientation; spell system and projectile non-goals; prefer more specific docs for concrete coding tasks |
| `docs/COMBAT_FEEDBACK_POLICY.md` | Combat feedback pipeline, floating damage text, hit event fields, collider rule for cosmetic objects |
| `docs/TEST_HARNESS.md` | Running EditMode/PlayMode tests; understanding test commands and results |
| `docs/PR_POST_OPEN_AGENT_LOOP.md` | After a PR is opened: parent ↔ CI/review subagent loop until merge-ready (GitHub MCP only; no agent merge) |

---

## Common context bundles

### Combat / missiles
- `docs/architecture.md` (Spell system section + projectile non-goals)
- `docs/COMBAT_FEEDBACK_POLICY.md`
- `docs/TEST_HARNESS.md`
- `Assets/Scripts/Combat/NetworkCombatController.cs`
- `Assets/Scripts/Combat/ActiveSpellInstanceRegistry.cs`
- `Assets/Scripts/Combat/ActiveSpellInstance.cs`
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
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/`
- `tools/` (test runner scripts)

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

The following documents existed in `docs/` at various points and were removed as part of documentation hygiene. Do **not** recreate or treat them as current guidance.

- `docs/TEST_COVERAGE_PLAN.md` — historical test planning and backlog; the tests it described are now implemented.
- `docs/CODE_CLEANUP_AUDIT.md` — one-time audit; findings resolved.
- `docs/FusionSharedTutorial-Plan.md` — implementation plan; feature complete.
- `docs/combat-travel-checkpoint.md` — mid-feature checkpoint; superseded.
- `docs/PROJECTILE_POLICY.md` — described the old `PlayerMissileSlot` single-slot model; superseded by `ActiveSpellInstanceRegistry`. Durable rules (collider rule, projectile non-goals) are now in `docs/COMBAT_FEEDBACK_POLICY.md` and `docs/architecture.md`.
