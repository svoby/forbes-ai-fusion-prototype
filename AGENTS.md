# Agent Coding Constraints — Forbes AI Fusion Prototype

These rules apply to every AI agent (Copilot, Cursor, Claude, GPT, etc.) contributing to this repository.

---

## Authority Model

| Concern | Owner | Rule |
|---|---|---|
| Networked state mutation | StateAuthority (host by default) | Never call `[Networked]` property setters from InputAuthority-only code |
| Physics movement | StateAuthority | Use `Runner.DeltaTime` inside `FixedUpdateNetwork` only |
| Input collection | InputAuthority (each client) | Gather raw input in `OnInput`; never apply it directly to world state |
| UI reads | Any peer | Read `[Networked]` properties; never write them from UI |

## Simulation Rules

1. **Tick-based only.** All gameplay logic runs inside `FixedUpdateNetwork` (Fusion's fixed-tick callback). Never use `Update` for authoritative state changes.
2. **Deterministic deltas.** Use `Runner.DeltaTime` (not `Time.deltaTime`) for any tick-time calculation.
3. **Input structs.** Define a single `NetworkInputData` struct per player. It must contain only primitive/blittable fields (bool, float, Vector2, etc.). No reference types.
4. **No client-side prediction of authority actions.** Clients may predict their own position (via `InputAuthority` checks) but must not predict the results of damage, HP changes, or spawn/despawn.

## Networking Constraints

- No custom transport logic. Use Fusion's built-in relay or direct connection.
- No manual packet reordering or interpolation buffers. Use Fusion's `NetworkTransform` or `NetworkRigidbody` components.
- All `NetworkObject` spawns must originate from the **host** (`HasStateAuthority`).

## Code Constraints

- One responsibility per script. Max ~150 lines per file before splitting.
- No singletons with mutable networked state. Use Fusion's `NetworkBehaviour` instead.
- No `FindObjectOfType` at runtime. Wire references via `[SerializeField]` or Fusion's `Runner.Spawn` callback.
- No coroutines for tick-sensitive logic. Use tick counters (`TickTimer`) instead.

## Prohibited Patterns

- ❌ Custom physics engine or manual collision resolution
- ❌ Writing `[Networked]` state outside `StateAuthority`
- ❌ Using `Time.time` / `Time.deltaTime` for authoritative simulation
- ❌ Spawning `NetworkObject` from InputAuthority
- ❌ Any architecture that requires >1 RPC per player action (prefer state-driven design)

## What Agents Should Always Do

- Check `Object.HasStateAuthority` before mutating any `[Networked]` property.
- Reference the relevant task ticket in comments: `// Task #N`.
- Keep files under 150 lines; split by responsibility if exceeded.
- Write the manual test steps in the ticket before writing code.
