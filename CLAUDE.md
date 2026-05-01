# Claude Project Context: Unity Fusion Prototype

## Project Type
Small multiplayer action-RPG style prototype (WoW-like basics).

## Stack
- Unity (latest LTS)
- Photon Fusion
- C#

## Architecture Constraints
- Gameplay state changes are authoritative on Host/State Authority.
- Clients submit intent/input only.
- Gameplay simulation runs in network ticks (`FixedUpdateNetwork`).
- Keep gameplay logic out of render-only `Update`.

## Current Feature Scope
- Join room (host/client)
- Spawn network players
- Move and rotate player
- Tab target selection
- Instant spell damage
- HP sync, death, respawn
- Minimal UI (my HP, target HP)

## Explicit Non-Goals (for now)
- Custom transport/reorder/loss logic
- Full rollback/rewind implementation
- Inventory, quests, persistence, complex AI
- UI polish before core loop is stable

## Test Checklist (always run)
1. Two clients can connect to one room.
2. Both players can move and see each other.
3. Target selection works.
4. Spell applies damage only with valid authority and range.
5. Death and respawn are consistent.
