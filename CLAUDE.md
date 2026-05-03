# Claude Project Context: Unity Fusion Prototype

## Project type
Small multiplayer action-RPG style prototype (WoW-like basics).

## Stack
- Unity (LTS)
- Photon Fusion
- C#

## Architecture constraints
- Gameplay state changes are authoritative on **Host / State Authority**.
- Clients submit **input/intent** only; no client-authoritative combat outcomes.
- Simulation in network ticks (`FixedUpdateNetwork`); keep gameplay out of render-only `Update`.

## Current feature scope
- Join room (host/client); **networked player spawn, movement, look**.
- **Health, death, respawn**; **Tab** targeting; spell damage on validated paths.
- **Training dummy** with **`NetworkMobBrain`**: wander, **XZ facing**, **melee**, **aggro**, **chase**, **leash/return** (prototype; mob tick logic runs under state authority).
- Minimal UI (HP, cast bar, target HP / world selected-target bar).

## Testing
- **EditMode (`Forbes.Tests.EditMode`):** pure logic, no runner (e.g. `NetworkMobBrainLogic`, combat validation, HUD math).
- **PlayMode (`Forbes.Tests.PlayMode`):** Fusion **`GameMode.Single`** smokes; centralize coroutine helpers (`WaitUntil`, `WaitFrames`, `TeleportNetworkObjectForPlayModeSmokeTest`) in **`FusionPlayModeTestHelpers`** (avoid duplicate coroutine code in fixtures).

## Non-goals (for now)
- Custom transport/reorder semantics, full rollback, inventory/quests/persistence, heavy AI polish.

## Mandatory post-implementation diff audit

**Trigger:** Any feature that took **multiple fix iterations** before tests or behaviour stabilized.

**Rule:** Before marking work complete, **stop and audit the final diff**. Passing tests alone are insufficient; the change set must be **clean and scoped**.

The audit **must:**
- Classify substantive changes as **required**, **cleanup**, **suspicious**, or **unrelated**.
- **Not** add new behaviour during the audit — only analyse and recommend minimal cleanup.

**Must flag:** weakened tests; production code added primarily for tests; changed serialized defaults; unnecessary **public API** growth; **duplicate test helpers**; accidental **prefab/scene/meta** edits; **Fusion authority** mistakes; **`FindObjectsByType` / full-scene scans on hot simulation paths**.

Details: `.cursor/rules/post-feature-diff-audit.mdc`.

## Manual smoke checklist
1. Two clients in one room; both see movement.
2. Target selection works.
3. Spell damage respects authority and range.
4. Death and respawn stay consistent host + client.
