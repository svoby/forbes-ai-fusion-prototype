# Forbes AI Fusion Prototype

A fast vertical-slice multiplayer prototype built with **Unity LTS + Photon Fusion**.  
Goal: reach a 2-player playable POC within a single focused sprint.

---

## Scope

**In scope (Milestone 1)**

| Feature | Description |
|---|---|
| Room connect | Host/Client join via Fusion lobby |
| Player spawn | Authority-correct spawn on both peers |
| Movement | Tick-based input → server-authoritative position |
| Tab target | Nearest-enemy selection, synced via NetworkObject |
| Instant spell | One projectile-free spell (e.g. instant damage on target) |
| HP sync | Networked HP with authoritative damage |
| Death / Respawn | Simple timer-based respawn at fixed point |
| Minimal HUD | HP bar, target nameplate, room status label |

**Out of scope (Milestone 1)**

- Custom physics / collision solver
- Custom network transport or reorder logic
- Ability system / buff framework
- Persistent accounts or matchmaking
- Audio, VFX, animations beyond placeholder capsules
- Mobile / console builds

---

## Setup

### Prerequisites

- Unity **2022.3 LTS** (or latest 2022.3.x patch)
- Photon Fusion **2.x** SDK ([download](https://dashboard.photonengine.com))
- A free Photon App ID (create at [dashboard.photonengine.com](https://dashboard.photonengine.com))

### Steps

1. Clone this repository.
2. Open the project folder in Unity Hub → add project → open.
3. Import the Photon Fusion 2 SDK unitypackage into `Assets/Plugins/Fusion/`.
4. In Unity: **Assets → Fusion → Setup → Enter App ID** → paste your Photon App ID.
5. Open the scene `Assets/Scenes/Game.unity`.
6. Press **Play** to host, open a second editor instance (or build) and press **Play** to join.

---

## Evening Milestone Checklist

- [ ] Two peers connect to the same room (host + client)
- [ ] Each peer spawns a distinct player capsule at a fixed spawn point
- [ ] Player movement is smooth and authoritative on host
- [ ] Tab key cycles targets; current target is highlighted on both clients
- [ ] Pressing `Q` on a live target reduces target HP by 20 (authoritative)
- [ ] HP bar reflects live HP for both players
- [ ] Reaching 0 HP triggers a 5-second respawn countdown then respawns
- [ ] No desyncs or ghost objects after a full death/respawn cycle

---

## Definition of Done

A feature is **done** when:

1. It compiles with zero errors and zero warnings in Unity 2022.3 LTS.
2. It runs correctly in a two-peer session (host + one client) without desyncs.
3. Authority model is correct: only the **StateAuthority** mutates networked state.
4. A manual test walkthrough matching the acceptance criteria in the task ticket passes.
5. Code is committed with a descriptive message referencing the task (e.g. `feat(player): spawn authority #3`).

---

## Project Structure

```
Assets/
  Scenes/              # Unity scenes
  Scripts/
    Networking/        # Fusion runner bootstrap, connection logic
    Player/            # PlayerController, InputProvider, SpawnManager
    Combat/            # TargetSelector, SpellCaster, HealthSystem
    UI/                # HudController, HealthBar, TargetNameplate
  Prefabs/             # Player prefab, HUD canvas
  Settings/            # Fusion NetworkProjectConfig, Physics settings
docs/
  architecture/        # System design docs
  tasks/               # Sprint task tickets
  task-template.md     # Template for new feature tickets
```

---

## Contributing

See [AGENTS.md](AGENTS.md) for AI agent constraints and coding rules.
