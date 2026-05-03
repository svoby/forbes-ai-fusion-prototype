# Forbes AI Fusion Prototype — Architecture Overview

## Stack

| Layer | Technology |
|-------|-----------|
| Engine | Unity 6 LTS |
| Networking | Photon Fusion 2 (Shared Mode) |
| Input | Unity Input System (new) |
| Physics | Fusion PhysicsScene (separate from Unity default) |

---

## Key architecture rules

- **State authority owns all gameplay outcomes.** Clients send intent (input struct), never final results.
- **Simulation runs in `FixedUpdateNetwork`.** Never put gameplay logic in `Update` or `LateUpdate`.
- **One responsibility per MonoBehaviour.** Camera ≠ input ≠ combat ≠ movement.
- **Physics scene**: Fusion spawns network objects into its own `PhysicsScene`. Use `runner.GetPhysicsScene().Raycast()` for any physics query that needs to hit network objects. Fall back to `Physics.Raycast()` for non-networked scene objects (floor, etc.).

---

## Script map

```
Assets/Scripts/
├── Core/
│   ├── GameplayInput.cs          — INetworkInput struct (Move, LookYaw, Buttons, TargetId)
│   ├── IInputSource.cs           — interface: keyboard/gamepad adapter
│   ├── CheckerboardFloor.cs      — runtime 3×3 checkerboard floor builder
│   └── ForbesLog.cs              — conditional debug log categories
│
├── Player/
│   ├── PlayerMovement.cs         — tick-driven movement + facing (FixedUpdateNetwork)
│   ├── KeyboardInputSource.cs    — reads keyboard/mouse → IInputSource each frame
│   ├── ThirdPersonOrbitCamera.cs — WoW orbit camera, cursor management
│   ├── Health.cs                 — networked HP, death, respawn (NetworkBehaviour)
│   └── HealthView.cs             — render-side HP bar (subscribes to Health.IsDeadChanged)
│
├── Combat/
│   ├── Targetable.cs             — marker: this object can be targeted
│   ├── TargetingController.cs    — local client target selection (Tab, LMB click)
│   ├── TargetHighlight.cs        — golden ring under current target (LineRenderer)
│   ├── NetworkCombatController.cs— authoritative cast, GCD, per-spell cooldowns
│   ├── SpellRegistry.cs          — hardcoded spell definitions (SpellData structs)
│   ├── CombatValidator.cs        — stateless range/cooldown/target checks
│   └── CombatFailReason.cs       — enum: why a cast was rejected
│
├── Networking/
│   ├── PlayerSpawner.cs          — spawns local player on join
│   ├── FusionInputProvider.cs    — bridges IInputSource → Fusion OnInput callback
│   └── TrainingDummySpawner.cs   — editor-only: spawns one dummy after local player joins
│
├── Training/
│   └── TrainingDummy.cs          — colours the dummy, ensures collider + Targetable exist
│
└── UI/
    └── CombatHud.cs              — IMGUI debug overlay: HP, target, cast bar, cooldowns
```

---

## Input pipeline

```
Keyboard/mouse
    │
    ▼
KeyboardInputSource (MonoBehaviour, Update)
    │  MoveAxes, LookYaw, AlwaysFaceYaw, Consume*()
    ▼
FusionInputProvider (INetworkRunnerCallbacks.OnInput)
    │  GameplayInput struct → Fusion tick
    ▼
PlayerMovement.FixedUpdateNetwork   (movement + facing)
NetworkCombatController.FixedUpdateNetwork  (spells, GCD)
```

---

## Camera, cursor & movement (WoW-style)

**Canonical specification:** [.cursor/rules/controls-spec.mdc](../.cursor/rules/controls-spec.mdc) — execution order (`Update` / `LateUpdate` / Fusion ticks), orbit vs **`_charYaw`** / **`_orbitOffset`**, **`AlwaysFaceYaw`**, **`CharacterController`** rules, and **cursor policy** (**RMB** locks immediately; **LMB** only after ~**20 px** accumulated drag so click-target stays valid).

**Short reference:**

| Mouse | Rotation | Facing / move (tick) |
|-------|----------|----------------------|
| None | Idle; Q/E rotate **`Yaw`** | Body-forward move unless **`AlwaysFaceYaw`** (strafe/Q/E/arrows/RMB modes) |
| LMB only | Orbit lens (**`_orbitOffset`**) — LMB does **not** change **`Yaw`** | LMB-only orbit: move along body forward unless **`AlwaysFaceYaw`** (e.g. A/D strafe forces face to **`LookYaw`**) |
| RMB only / Both | Free-look: **`_charYaw`** from mouse; **Both** adds auto-forward | **`AlwaysFaceYaw`** true — move/strafe in **`LookYaw`** space |

`TargetingController` reads **`IsLmbDragging`** in **`Update`**; `ThirdPersonOrbitCamera.LateUpdate` clears it **after** that on LMB release (`IsRmbDragging` is mirrored for bookkeeping only; **cursor** follows **`rmb || (lmb && IsLmbDragging)`** in code).

---

## Target selection

1. **Tab** — cycles alive `Targetable` objects sorted by `NetworkId.Raw`, skips local player.
2. **LMB click** (< 20 px movement) — `runner.GetPhysicsScene().Raycast()` from camera through cursor position; `GetComponentInParent<Targetable>()` on hit; miss = keep current target.
3. **Escape** — clears target.

`FusionInputProvider.OnInput` copies `TargetingController.CurrentTargetId` into every `GameplayInput` tick so the state authority always knows the locally selected target.

---

## Spell system

`SpellRegistry` holds static `SpellData` (id, name, damage, range, cast time, GCD, cooldown).
`CombatValidator` is stateless — checks: target alive, in range, GCD clear, spell cooldown clear.
`NetworkCombatController` owns networked cooldown ticks and resolves damage via `Health.DealDamageRpc`.

---

## Milestone 1 status — DONE ✓

- [x] Host/client room connect
- [x] Player spawn
- [x] Movement + look rotation (WoW mouse modes)
- [x] Tab + click target selection
- [x] Spell 1/2/3 (instant damage, cast time stub)
- [x] HP sync, death, respawn
- [x] Minimal HUD (HP, target, cast bar, cooldowns)
- [x] 3×3 checkerboard test floor

## Next steps (Milestone 2)

- [ ] Replace IMGUI HUD with proper Canvas UI
- [ ] Spell visual effects (particles, projectiles)
- [ ] Cast-time spells (animation + interrupt)
- [ ] Second player test (two clients in same room)
- [ ] Training dummy with basic AI (patrol, aggro radius)
- [ ] Remove legacy `PlayerCombat.cs` stub
