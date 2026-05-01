# Networking Architecture

## Overview

This project uses **Photon Fusion 2** in **Shared Mode** with a single host acting as State Authority for all gameplay objects.  
Topology: Host ↔ Relay ↔ Clients. No dedicated server in Milestone 1.

---

## Topology

```
[Player A — Host]
    ↕  Fusion Relay
[Player B — Client]
```

- **Host** = StateAuthority for all `NetworkObject`s spawned by `Runner.Spawn`.
- **Client** = InputAuthority for its own player object; reads all other networked state.
- Fusion's interest management is disabled (2-player game, everything is always relevant).

---

## Tick Model

| Property | Value |
|---|---|
| Simulation mode | `Fusion.SimulationModes.Host` |
| Tick rate | 60 Hz (configurable in `NetworkProjectConfig`) |
| Time model | `Runner.DeltaTime` (fixed, not wall-clock) |
| Reconciliation | Fusion built-in client prediction + reconciliation |

All authoritative gameplay logic lives in `FixedUpdateNetwork`.  
`Update` is used only for rendering/UI interpolation.

---

## Input Flow

```
Client frame:
  INetworkRunnerCallbacks.OnInput()
      → fill NetworkInputData struct
      → Fusion sends to host each tick

Host tick (FixedUpdateNetwork):
  GetInput(out NetworkInputData input)
      → apply movement, ability triggers
      → mutate [Networked] state
```

### NetworkInputData (canonical struct)

```csharp
public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;       // WASD / stick
    public NetworkBool Jump;
    public NetworkBool CastSpell; // Q key — instant spell
    public NetworkBool TabTarget; // Tab key — cycle target
}
```

---

## State Synchronization

| Component | Mechanism |
|---|---|
| Position / Rotation | `NetworkTransform` (built-in interpolation) |
| HP | `[Networked] int HP` on `HealthSystem : NetworkBehaviour` |
| Current target | `[Networked] NetworkObject Target` on `TargetSelector` |
| Respawn timer | `[Networked] TickTimer RespawnTimer` on `HealthSystem` |

### Authority Rules

| Action | Authority Check |
|---|---|
| Apply damage | `HasStateAuthority` |
| Spawn player | `HasStateAuthority` (host only) |
| Set target | `HasInputAuthority` (client predicts, host confirms) |
| Trigger respawn | `HasStateAuthority` |

---

## Key Scripts

| Script | Folder | Responsibility |
|---|---|---|
| `NetworkBootstrap` | `Networking/` | Start Fusion runner, join/create room |
| `PlayerInputProvider` | `Player/` | Implement `INetworkRunnerCallbacks.OnInput` |
| `PlayerController` | `Player/` | Movement in `FixedUpdateNetwork` |
| `SpawnManager` | `Player/` | Spawn players on peer join |
| `HealthSystem` | `Combat/` | HP, damage, death, respawn timer |
| `TargetSelector` | `Combat/` | Tab-target logic |
| `SpellCaster` | `Combat/` | Instant spell execution |
| `HudController` | `UI/` | Read networked state; update UI elements |

---

## Sequence: Room Connect → First Tick

```
1. NetworkBootstrap.StartGame(GameMode.AutoHostOrClient)
2. Fusion: OnConnectedToServer → OnSessionListUpdated / OnPlayerJoined
3. SpawnManager.OnPlayerJoined → Runner.Spawn(playerPrefab, spawnPoint, ...)
4. Fusion: NetworkObject.Spawned on all peers
5. FixedUpdateNetwork begins; inputs flow; state syncs
```

---

## Constraints (do not violate)

- No RPC for damage — use `[Networked]` HP + `HasStateAuthority` guard.
- No manual interpolation buffer — use `NetworkTransform`.
- No custom transport — use Fusion's built-in relay.
