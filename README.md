# Forbes AI Fusion Prototype

Unity multiplayer vertical-slice prototype using Photon Fusion.

## Goal (Current)

Build a fast playable POC with:
- Host + Client room connect
- Player spawn
- Movement + rotation
- Tab target selection
- One instant spell
- HP sync, death, respawn
- Minimal HUD

## Tech

- Unity (latest LTS)
- Photon Fusion
- C#

## Scope

### In Scope
- 2-player gameplay loop for internal testing
- Authority-correct damage flow
- Basic combat feedback through HP/UI

### Out of Scope (Milestone 1)
- Custom physics engine
- Manual packet order/loss handling
- Full rollback/rewind stack
- Inventory/quests/persistence

## Setup (Quick)

1. Install Unity LTS via Unity Hub.
2. Create or open project in this repo.
3. Install Photon Fusion SDK.
4. Configure Photon AppId.
5. Run two clients (Host + Client) and verify room join.

## Development Rules

- State changes on Host/State Authority only.
- Clients send input/intent only.
- Tick-based gameplay in `FixedUpdateNetwork`.
- Keep features small and test after each step.
