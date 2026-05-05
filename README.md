# Forbes AI Fusion Prototype

Unity multiplayer vertical-slice prototype using Photon Fusion.

## Current State

A playable host+client room with third-person movement, tab-targeting, spell combat, mob AI, and a minimal HUD.

### Implemented Features

| Area | Detail |
|---|---|
| **Networking** | Host/client room; networked player spawn |
| **Movement** | `CharacterController`-based third-person movement with WoW-style orbit camera |
| **Targeting** | Tab-cycle target selection; target highlight; world-space target HP bar |
| **Combat** | Cast bar, spell validation (`CombatValidator`), projectile travel (`SpellTravelLogic`), networked damage (`NetworkCombatController`); death + respawn |
| **Mob AI** | `NetworkMobBrain` — wander, XZ facing, aggro/chase, melee, leash/return (`TrainingDummy` prefab) |
| **HUD** | Player HP bar (`HealthView`), cast bar (`CastBarView`), selected-target info bar (`SelectedTargetHealthBar`), Fusion stats toggle |

### Script Layout

```
Assets/Scripts/
  Combat/          CastCancelReason, CombatFailReason, CombatFeedbackReason,
                   CombatValidator, CosmeticProjectileView, NetworkCombatController,
                   SpellImpactView, SpellRegistry, SpellTravelLogic, SpellVisualColors,
                   Targetable, TargetHighlight, TargetingController
  Core/            CheckerboardFloor, ForbesLog, GameplayInput, IInputSource
  Mobs/            NetworkMobBrain, NetworkMobBrainLogic
  Networking/      FusionInputProvider, PlayerSpawner, TrainingDummySpawner
  Player/          Health, HealthView, HitImpactView, KeyboardInputSource,
                   PlayerColor, PlayerMovement, ThirdPersonOrbitCamera
  Training/        TrainingDummy
  UI/              CastBarView, CombatHud, CombatWarningText, FloatingCombatTextCanvas,
                   FloatingCombatTextItem, FloatingCombatTextLogic, FusionHudToggle,
                   SelectedTargetHealthBar, TargetHealthBarLogic
```

### Tests

**EditMode** (`Forbes.Tests.EditMode`) — pure logic, no runner:
- `CombatValidatorPureTests`, `CombatFailReasonEnumTests`
- `CombatFeedbackReasonEnumTests`, `CombatFeedbackMappingTests`, `CombatHudFeedbackVisibilityTests`, `MidCastCancelPolicyTests`
- `CombatHitEventTests`
- `CombatWarningTextTests`
- `NetworkCombatSecsToTicksTests`
- `SpellRegistryTests`, `SpellTravelLogicTests`
- `NetworkMobBrainLogicTests`
- `CastBarLayoutDefaultsTests`, `CastBarHudRegressionTests`
- `HealthDefaultsTests`, `TargetHealthBarLogicTests`
- `CosmeticProjectileViewColliderTests`
- `FloatingCombatTextLogicTests`

**PlayMode** (`Forbes.Tests.PlayMode`) — Fusion `GameMode.Single` smokes:
- `FusionHealthSmokeTests`
- `CombatFeedbackSmokeTests`
- `FusionSinglePlayerTestSession` (session fixture)
- `NetworkMobBrainMovementSmokeTests`, `NetworkMobBrainMeleeSmokeTests`, `NetworkMobBrainChaseLeashSmokeTests`
- `NetworkCombatProjectileTravelSmokeTests`
- `TargetableTests`, `TargetHighlightTests`, `PlayModeTargetingCleanup`
- `HealthViewTests`, `SelectedTargetHealthBarSmokeTests`

Shared helpers: `FusionPlayModeTestHelpers`, `FusionPlayModeTestInputRelay`, `FusionPlayModeTestAssets`.

## Tech

- Unity (latest LTS)
- Photon Fusion
- C#

## Scope

### In Scope
- 2-player gameplay loop (host + one client)
- Authority-correct damage and mob state
- Basic combat feedback through HP/HUD

### Out of Scope (Milestone 1)
- Custom physics / manual packet ordering
- Full rollback/rewind stack
- Inventory, quests, persistence

## Setup

1. Install Unity LTS via Unity Hub and open this repo.
2. Install Photon Fusion SDK.
3. Configure your Photon AppId (`PhotonAppSettings`).
4. Run two clients (Host + Client) and verify room join.

## Running Tests

### Inside the Editor
- **Window → General → Test Runner → Edit Mode** → select `Forbes.Tests.EditMode` → **Run All**.
- Switch to **Play Mode** tab for PlayMode smokes.

### CLI (Editor must be closed)

```powershell
# EditMode
powershell -ExecutionPolicy Bypass -File tools\run-editmode-tests.ps1

# PlayMode
powershell -ExecutionPolicy Bypass -File tools\run-playmode-tests.ps1
```

Results: `TestResults/editmode.xml` / `TestResults/playmode.xml`.
Logs: `TestResults/unity-editmode.log` / `TestResults/unity-playmode.log`.

## Architecture Rules

- Gameplay state changes are authoritative on **Host / State Authority only**.
- Clients submit **input/intent** — never authoritative combat outcomes.
- Core simulation runs in `FixedUpdateNetwork`; keep gameplay out of render-only `Update`.
- One responsibility per `MonoBehaviour`; avoid god classes.

## Manual Smoke Checklist

1. Two clients in one room; both see movement.
2. Tab-targeting cycles players and dummies; highlight appears on target.
3. Spell damage respects range validation and hits only on host authority.
4. Death and respawn stay consistent on host and client.
5. Training dummy aggroes, chases, melees, and leashes correctly.
