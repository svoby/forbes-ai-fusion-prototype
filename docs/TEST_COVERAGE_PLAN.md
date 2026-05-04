# Test Coverage Plan — Forbes AI Fusion Prototype

Status: drafted from a read-only inspection of the current vertical slice.
Scope: protect existing behaviour described in [README.md](../README.md), [AGENTS.md](../AGENTS.md), [CLAUDE.md](../CLAUDE.md), and [docs/architecture.md](architecture.md). This plan does **not** propose gameplay changes.

Unity Test Framework: present (`com.unity.test-framework@1.6.0`) in [Packages/manifest.json](../Packages/manifest.json). **Note: this plan was originally drafted before the test infrastructure existed.** `Assets/Tests/EditMode/` (assembly `Forbes.Tests.EditMode`) and `Assets/Tests/PlayMode/` (assembly `Forbes.Tests.PlayMode`) now exist; `Assets/Scripts/Forbes.Runtime.asmdef` is also present. The folder layout and asmdef setup described in sections D and F are complete.

---

## A. Current architecture summary

### Input flow

[`KeyboardInputSource`](../Assets/Scripts/Player/KeyboardInputSource.cs) (MonoBehaviour, `Update`) reads `Keyboard.current` / `Mouse.current` directly and exposes [`IInputSource`](../Assets/Scripts/Core/IInputSource.cs):

- `MoveAxes` (Vector2: A/D strafe + W/S/arrows forward-back; Both-mouse forces `moveY >= 1`).
- `LookYaw` (mirrors `ThirdPersonOrbitCamera.Yaw`).
- `AlwaysFaceYaw` (true on RMB held, Q/E/arrows, or A/D strafe).
- `ConsumeJump`, `ConsumeSpell1/2/3`, `ConsumeRandomizeColor` — edge-press latches drained exactly once per consumer call.

[`FusionInputProvider`](../Assets/Scripts/Networking/FusionInputProvider.cs) is `INetworkRunnerCallbacks` on the `NetworkRunner` GameObject. In `OnInput` it copies the sibling `IInputSource` values into a `GameplayInput`, drains the Consume-edge flags into `NetworkButtons`, and copies `TargetingController.CurrentTargetId` into `gi.TargetId`.

### Movement flow

[`PlayerMovement`](../Assets/Scripts/Player/PlayerMovement.cs) is `NetworkBehaviour`. In `FixedUpdateNetwork`:

1. Returns immediately if not state authority, controller missing, dead, or `_combat.IsCasting`.
2. `GetInput(out GameplayInput input)` — bails on missing input.
3. Computes `moveYaw` from `AlwaysFaceYaw` (true → camera yaw; false → body forward).
4. Applies gravity, optional jump on `Jump` edge-press while grounded, then `_controller.Move(...)`.
5. Sets `transform.forward` only when `AlwaysFaceYaw` is set.

Constants are inlined: `PlayerSpeed = 10f`, `JumpForce = 5f`, `GravityValue = -9.81f`. `LateUpdate` does input-authority-only render-rate facing using the live camera yaw and `KeyboardInputSource.AlwaysFaceYaw`.

### Targeting flow

[`TargetingController`](../Assets/Scripts/Combat/TargetingController.cs) (MonoBehaviour, auto-created via `[RuntimeInitializeOnLoadMethod]` in `AutoCreate`):

- Tab → `CycleTarget` enumerates `Object.FindObjectsByType<Targetable>(...)`, filters out the local player (`PlayerMovement.HasInputAuthority`) and dead targets (`Health.IsDead`), sorts by `NetworkObject.Id.Raw`, advances index by 1.
- LMB release with no drag (`!_camera.IsLmbDragging`) → `TrySelectFromScreenRay` raycasts via `_runner.GetPhysicsScene().Raycast(...)` first, falls back to `Physics.Raycast`.
- Escape → `SetTarget(null)`.
- `CurrentTargetId` returns `NetworkObject.Id` or `default` if no target.

[`Targetable`](../Assets/Scripts/Combat/Targetable.cs) requires `NetworkObject` and exposes `DisplayName` (falls back to `gameObject.name`). [`TargetHighlight`](../Assets/Scripts/Combat/TargetHighlight.cs) is a singleton `LineRenderer` ring; `SetTarget(t)` toggles `gameObject.SetActive(t != null)` and `LateUpdate` snaps to the target's feet.

### Combat flow

[`NetworkCombatController`](../Assets/Scripts/Combat/NetworkCombatController.cs):

- `[Networked]`: `CurrentSpellId` (byte), `CastTarget` (NetworkId), `CastStartTick`, `CastEndTick`, `GcdEndTick`, `Cooldown1/2/3EndTick`, `LastFailReason`, `LastFailTick`, `PendingMissileReleaseTick`.
- `IsCasting => CurrentSpellId != 0 && Runner.Tick < CastEndTick`.
- `FixedUpdateNetwork`: state-auth only; if dead → `ClearCastState`; if casting and `Tick >= CastEndTick` → `ResolveCast`; advances any in-flight missile each tick via `SpellTravelLogic.AdvanceMissilePosition` (updating the authority-only `_missileVirtualPos`); when `SpellTravelLogic.HasMissileArrived` returns true, clears the missile slot and calls `Health.DealDamageRpc`; else reads input and dispatches on `Spell1/2/3` edge.
- `TryStartCast(spellId, targetId)` calls [`CombatValidator.TryValidate`](../Assets/Scripts/Combat/CombatValidator.cs); on success arms GCD, then for instant spells calls `targetHealth.DealDamageRpc(spell.Damage)` directly; for projectile spells sets `PendingMissileReleaseTick` and initialises `_missileVirtualPos` at the caster position (damage is deferred until missile arrival, not applied at cast completion); for cast-time spells stores cast state and pre-bakes cooldown that begins at cast start (matches WoW: cancellation does not bypass CD).
- `ResolveCast` re-validates (target may have died/walked) and either applies damage immediately (instant/cast-time) or releases the missile (projectile spells).
- `_missileVirtualPos` is **non-networked and authority-only**; clients do not receive the missile position directly. See [`docs/PROJECTILE_POLICY.md`](PROJECTILE_POLICY.md).
- `SecsToTicks(seconds) => Mathf.CeilToInt(seconds * Runner.TickRate)` (private).
- Constant: `GcdSec = 1.0f`.

[`CombatValidator.TryValidate`](../Assets/Scripts/Combat/CombatValidator.cs) is `static` and stateless except for one Fusion call: `runner.TryFindObject(targetId, out var targetObj)`. Order of rejection (load-bearing): `AlreadyCasting` → `GcdActive` (only when `spell.TriggersGcd`) → `OnCooldown` → `NoTarget` (invalid id, lookup miss, or no `Health` sibling) → `TargetDead` → `OutOfRange`.

[`SpellRegistry`](../Assets/Scripts/Combat/SpellRegistry.cs) is a static array of immutable `SpellData` records. Three entries today: id 1 Fireball (cast 1.5 s, cd 0, range 30, dmg 30, GCD), id 2 Arcane Shot (instant, cd 3, range 25, dmg 15, GCD), id 3 Heavy Blast (cast 2.5 s, cd 8, range 30, dmg 60, GCD). `Get(id)` returns `default` (invalid) for `id < 1` or `id > All.Length`.

### Health / death / respawn flow

[`Health`](../Assets/Scripts/Player/Health.cs):

- `[Networked]`: `NetworkedHealth` (float), `IsDead` (NetworkBool, `OnChangedRender(nameof(OnIsDeadChangedRender))` → fires `IsDeadChanged` event), `RespawnAtTick` (int), `SpawnPosition` (Vector3).
- Public: `StartingHealth = 100f`, `RespawnDelaySeconds = 3f`.
- `Spawned()` (state auth) records `SpawnPosition`, sets `NetworkedHealth = StartingHealth`; always invokes `IsDeadChanged?.Invoke(IsDead)` so render subscribers can sync.
- `FixedUpdateNetwork`: state-auth only; if `IsDead` and `RespawnAtTick != 0` and `Runner.Tick >= RespawnAtTick` → `Respawn()` (disable `CharacterController` around `transform.position = SpawnPosition`, restore HP, clear `IsDead`, `RespawnAtTick = 0`).
- `DealDamageRpc(damage)` is `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]`; ignored if not state auth or already dead. On HP <= 0 sets `IsDead = true` and `RespawnAtTick = Runner.Tick + Mathf.Max(1, Mathf.CeilToInt(RespawnDelaySeconds * Runner.TickRate))`.

### Spawning flow

[`PlayerSpawner`](../Assets/Scripts/Networking/PlayerSpawner.cs) on the runner GameObject. `OnPlayerJoined` ignores remote players, `runner.Spawn(PlayerPrefab, ...)` for the local player, `runner.SetPlayerObject(...)`, raises `LocalPlayerSpawned`. [`TrainingDummySpawner`](../Assets/Scripts/Networking/TrainingDummySpawner.cs) is editor-only (gated by `Application.isEditor && spawnInEditor`); after `LocalPlayerSpawned`, `CoSpawnTrainingDummy` waits up to 32 frames for the local player object, then `runner.Spawn(TrainingDummyPrefab, world, rot, PlayerRef.None, null, NetworkSpawnFlags.SharedModeStateAuthLocalPlayer)`.

### UI / HUD flow

[`CombatHud`](../Assets/Scripts/UI/CombatHud.cs) is IMGUI on the runner GameObject. Each frame it reads:
`runner.TryGetPlayerObject(LocalPlayer)` → `Health` for self HP and `NetworkCombatController` for cast bar (`IsCasting`, `CastProgress`, `CurrentSpellId`), GCD remaining (`GcdEndTick - Runner.Tick`), per-spell CDs, and `LastFailReason` (visible for 2 s).
[`HealthView`](../Assets/Scripts/Player/HealthView.cs) subscribes to `Health.IsDeadChanged` and toggles every `Renderer` under the character root (body + prefab children such as “eyes”).

---

## B. Risk map (most fragile first)

| # | Area | Why fragile | Regression looks like | Test type |
|---|------|-------------|-----------------------|-----------|
| 1 | `CombatValidator.TryValidate` rejection order | The exact ordering encodes UX (e.g. AlreadyCasting beats GCD; NoTarget beats Range). A reorder by a careless edit can mask cheats or surface the wrong HUD reason. | Wrong `LastFailReason` shown; clients can spam-cast while on GCD. | EditMode pure (with the seam in section F). |
| 2 | `Health.DealDamageRpc` authority + double-death | Guarded by `HasStateAuthority` and early `IsDead` return. Removing either lets clients overwrite HP or schedule duplicate respawns. | Damage applied on non-authority; multiple respawns; HP underflow if `Mathf.Max(0,...)` is removed. | PlayMode Fusion smoke (Single mode). |
| 3 | Tick math | Two formulas: `SecsToTicks = Mathf.CeilToInt(seconds * Runner.TickRate)` (private) and `RespawnAtTick = Runner.Tick + Mathf.Max(1, Mathf.CeilToInt(RespawnDelaySeconds * Runner.TickRate))`. Off-by-one or rounding drift will silently change feel. | Spells resolve a tick early/late; respawn fires immediately. | EditMode parametric (needs the `internal` seam in F). |
| 4 | `GameplayInput.TargetId` propagation | Three hops (`TargetingController.CurrentTargetId` → `FusionInputProvider.OnInput` → `NetworkCombatController.TryStartCast(targetId)`). Any hop returning `default` causes silent NoTarget rejections. | Spells fail with `NoTarget` despite a visible ring. | PlayMode component test with fakes + Fusion smoke. |
| 5 | Cast resolution at `CastEndTick` + ~~legacy `PlayerCombat` still on prefab~~ | **Status: resolved.** `PlayerCombat` component removed from `PlayerCharacter.prefab` (was referenced by GUID `c7d8e9f0...` in `m_Component` and `NetworkedBehaviours` lists; the class-name grep in the audit missed this). `PlayerCombat.cs` deleted from repo. `ResolveCast` re-validation in `NetworkCombatController` remains essential. | Double damage; cast that should fail at completion still fires. | Fusion smoke remains valid to guard `ResolveCast` re-validation independently of `PlayerCombat`. |
| 6 | `PlayerMovement` freeze while casting / dead | Two early returns. If the order is reshuffled (e.g. dead check after movement), a dead player can drift. | Dead/casting players slide around. | PlayMode test against the field interactions. |
| 7 | Tab cycle determinism | Sort by `NetworkObject.Id.Raw` (uint compare). If list construction or sort changes, cycle order becomes unstable across clients. | Tab targets in different order across machines, breaks demo. | EditMode pure list-based test (with a seam to inject the targetable enumeration). |
| 8 | `KeyboardInputSource` Consume* idempotency | Each spell key sets a latch in Update; `Consume*` returns true once and resets. If `Consume*` returns true twice the player double-casts. | Single keystroke fires two spells across two ticks. | EditMode pure (the latches are private bools but `Consume*` is the public surface). |
| 9 | `TrainingDummySpawner` race | Up to 32-frame coroutine waiting for `runner.IsRunning` and `LocalPlayer` object. Editor-only gating means it never runs in builds — the smoke test must spawn the dummy itself. | No dummy in the scene → smoke tests time out. | PlayMode smoke that bypasses the spawner. |
| 10 | `TargetingController` auto-bootstrap and `Camera.main` discovery | `[RuntimeInitializeOnLoadMethod]` on both `TargetingController` and `ThirdPersonOrbitCamera` rely on global state. Tests can pollute each other. | Tests pass solo, fail in batch. | PlayMode tests must tear down auto-created singletons. |

---

## C. Test priority list

### P0 — must add first (cheap, high signal)
- `SpellRegistryTests` — pin every spell field and the bounds of `Get`.
- `CombatValidatorPureTests` — pin the rejection order for everything that does NOT need a `NetworkRunner`.
- `HealthDefaultsTests` — pin `StartingHealth = 100`, `RespawnDelaySeconds = 3`, and the public surface (`StartingHealth`, `RespawnDelaySeconds`, `IsDeadChanged` event signature).
- `CombatFailReasonEnumTests` — pin numeric values of `CombatFailReason` because it crosses the network as `byte` (see `LastFailReason`).
- `SpellTravelLogicTests` — pin pure missile math in `SpellTravelLogic`: `AdvanceMissilePosition` step size given speed/delta, `HasMissileArrived` threshold boundary cases, behavior when target moves between advance calls. No runner required. These are the primary regression guard for the authoritative missile math; see [`docs/PROJECTILE_POLICY.md`](PROJECTILE_POLICY.md).

### P1 — should add next (component scope)
- `TargetableTests` (PlayMode) — `DisplayName` fallback to `gameObject.name`; respects serialised override.
- `TargetHighlightTests` (PlayMode) — `Awake` deactivates; `SetTarget(t)` activates and tracks; `SetTarget(null)` deactivates.
- `HealthViewTests` (PlayMode) — toggling `Health.IsDeadChanged` flips all `Renderer.enabled` in the hierarchy; `OnDisable` unsubscribes.
- `KeyboardInputSourceConsumeTests` (PlayMode) — exercises only the `IInputSource` Consume* contract through a `FakeInputSource` test double, since the real one reads `Keyboard.current`.
- `FusionInputProviderTests` (PlayMode, no runner) — feed a `FakeInputSource` + a tiny `TargetingController` substitute and assert the `GameplayInput` written to `NetworkInput` (this needs a small seam — see F).

### P2 — Fusion vertical slice smoke
- `FusionVerticalSliceSmokeTests` (PlayMode) — start runner in `GameMode.Single`, spawn `PlayerCharacter.prefab` and `TrainingDummy.prefab`, deal lethal damage via `Health.DealDamageRpc(100)`, await `IsDead == true`, await `RespawnAtTick`, assert HP restored at `SpawnPosition`. Use `LogAssert.NoUnexpectedReceived()`.
- **Moving-target projectile smoke** — release a projectile spell against a target that moves during flight; assert that `PendingMissileReleaseTick` is set after cast resolution, that `_missileVirtualPos` (via an `internal` seam or observable side-effect) advances each tick, and that `Health.NetworkedHealth` decreases only after `HasMissileArrived` becomes true (not at cast completion). Confirm damage is applied by State Authority. See [`docs/PROJECTILE_POLICY.md`](PROJECTILE_POLICY.md) for the full missile model.

### Manual-only (not automated)
- Camera feel, mouse drag thresholds, cursor lock policy.
- HUD readability (IMGUI overlay).
- Animation feel; eye placement.
- Two real clients on different machines / Photon AppId behavior.
- Visual polish, colours, lighting.

---

## D. Concrete proposed test files

Create only when implementing tests (this plan does not author them):

- `Assets/Tests/EditMode/Forbes.Tests.EditMode.asmdef` — references: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `Assembly-CSharp`, `Fusion.Runtime`. Test platforms: editor only.
- `Assets/Tests/EditMode/SpellRegistryTests.cs`
- `Assets/Tests/EditMode/CombatValidatorPureTests.cs`
- `Assets/Tests/EditMode/HealthDefaultsTests.cs`
- `Assets/Tests/EditMode/CombatFailReasonEnumTests.cs`
- **PlayMode / component tests** — *not present in this repository today;* if added later, use a dedicated `Assets/Tests/PlayMode/` assembly (see sections P1/P2 and folder sketch below).
- `Assets/Tests/Common/FakeInputSource.cs` — a `MonoBehaviour` implementing [`IInputSource`](../Assets/Scripts/Core/IInputSource.cs) for tests; keep out of production asmdefs.

---

## E. Per-file test cases (Given / When / Then)

### `SpellRegistryTests.cs`
- Given any `byte id` outside `1..All.Length`, when `SpellRegistry.Get(id)`, then `IsValid` is false.
- Given `id = 1`, when `Get(1)`, then `Name == "Fireball"`, `CastTimeSec == 1.5f`, `CooldownSec == 0f`, `RangeMeters == 30f`, `Damage == 30f`, `TriggersGcd == true`.
- Given `id = 2`, when `Get(2)`, then `Name == "Arcane Shot"`, `CastTimeSec == 0f`, `CooldownSec == 3f`, `RangeMeters == 25f`, `Damage == 15f`, `TriggersGcd == true`.
- Given `id = 3`, when `Get(3)`, then `Name == "Heavy Blast"`, `CastTimeSec == 2.5f`, `CooldownSec == 8f`, `RangeMeters == 30f`, `Damage == 60f`, `TriggersGcd == true`.
- Given the static table, when iterating `SpellRegistry.All`, then `All[i].Id == i + 1` for every entry and `All.Length == 3`.

### `CombatValidatorPureTests.cs`
Note: the current public signature requires a `NetworkRunner`. These tests assume the seam in F (`TryValidate(Transform, NetworkId, SpellData, currentTick, gcdEndTick, cooldownEndTick, isAlreadyCasting, Health resolvedTarget, out CombatFailReason)`) — until the seam exists, mark these tests `[Ignore("blocked on seam-1")]`.
- Given `isAlreadyCasting = true`, when `TryValidate(...)`, then result is false and `failReason == AlreadyCasting` regardless of all other state.
- Given `spell.TriggersGcd = true` and `currentTick < gcdEndTick`, when `TryValidate(...)`, then `failReason == GcdActive`.
- Given `spell.TriggersGcd = false`, when `currentTick < gcdEndTick`, then GCD is ignored (the next condition is evaluated).
- Given `currentTick < cooldownEndTick`, when above checks pass, then `failReason == OnCooldown`.
- Given `targetId.IsValid == false`, when above checks pass, then `failReason == NoTarget`.
- Given a resolved target with `Health.IsDead == true`, when above checks pass, then `failReason == TargetDead`.
- Given caster and target distance > `spell.RangeMeters`, when above checks pass, then `failReason == OutOfRange`.
- Given all checks pass, then result is true and `failReason == None`.

### `HealthDefaultsTests.cs`
- Given a freshly constructed `Health` (no `Spawned()` called), when reading public fields, then `StartingHealth == 100f` and `RespawnDelaySeconds == 3f`.
- Given the type `Health`, when reflecting `IsDeadChanged`, then it is a public `event Action<bool>` (catches accidental rename / signature drift).
- Given the `[Networked]` properties, when reflecting `Health`, then `NetworkedHealth`, `IsDead`, `RespawnAtTick`, `SpawnPosition` exist with public setters (the network code-gen relies on this).

### `CombatFailReasonEnumTests.cs`
- Given the enum, when casting each value to `byte`, then the numeric mapping is exactly: `None=0`, `NoTarget=1`, `OutOfRange=2`, `TargetDead=3`, `OnCooldown=4`, `GcdActive=5`, `AlreadyCasting=6`, `CasterDead=7`. (This crosses the wire as a byte — renumbering corrupts every running session.)

### `TargetableTests.cs` (PlayMode)
- Given a `GameObject` with `NetworkObject` + `Targetable` + no `_displayName` set, when reading `DisplayName`, then it returns `gameObject.name`.
- Given the same with `_displayName = "Boss"`, when reading `DisplayName`, then it returns `"Boss"`.
- Given a `Targetable` whose `NetworkObject` is missing, when reading `NetworkObject`, then it lazily caches via `GetComponent` (assert the `RequireComponent` is honored by Unity).

### `TargetHighlightTests.cs` (PlayMode)
- Given a fresh `TargetHighlight` after `Awake`, when reading `gameObject.activeSelf`, then it is false.
- Given the `Instance` after `Awake`, when calling `SetTarget(targetable)`, then `gameObject.activeSelf` becomes true and `LateUpdate` snaps `transform.position` to `target.position + Vector3.up * 0.04f`.
- Given an active highlight, when calling `SetTarget(null)`, then `gameObject.activeSelf` becomes false within one frame.
- Given two `TargetHighlight` instances created in sequence, when reading `Instance`, then it equals the most-recently-awake one (asserts singleton override behaviour).

### `HealthViewTests.cs` (PlayMode)
- Given a `GameObject` with `Health` + `HealthView` + `MeshRenderer`, when invoking `Health.IsDeadChanged?.Invoke(true)` via reflection, then `_renderer.enabled == false`.
- Given the same after `IsDeadChanged?.Invoke(false)`, then `_renderer.enabled == true`.
- Given a child `MeshRenderer` under the same root, when `Invoke(true)`, then it is disabled together with the root renderer.
- Given a disabled `HealthView`, when raising `IsDeadChanged`, then no exception is raised and the renderer state is untouched (handler unsubscribed in `OnDisable`).

### `KeyboardInputSourceConsumeTests.cs` (PlayMode, via `FakeInputSource`)
- Given a `FakeInputSource` with `_pendingSpell1 = true`, when calling `ConsumeSpell1` once, then result is true.
- Given the same source, when calling `ConsumeSpell1` a second time without re-arming, then result is false (regression guard for double-cast).
- Given a `FakeInputSource` with `_pendingJump = true`, when calling `ConsumeSpell1` first, then `ConsumeJump` still returns true (latches are independent).

### `FusionInputProviderTests.cs` (PlayMode, no runner)
Note: `FusionInputProvider.OnInput` writes to `NetworkInput input.Set(gi)`. A direct call needs either Fusion's `NetworkInput` constructor (available) or a tiny seam — see F.
- Given a `FusionInputProvider` with sibling `FakeInputSource` (Move = (0.4, -0.3), LookYaw = 90, Spell1 pending) and no targeting, when `OnInput` is invoked, then the resulting `GameplayInput.Move == (0.4, -0.3)`, `LookYaw == 90`, `TargetId == default`, `Buttons.IsSet(Spell1) == true`, and `_pendingSpell1` is now drained.
- Given the same with `AlwaysFaceYaw = true`, when `OnInput` runs, then `Buttons.IsSet(AlwaysFaceYaw) == true` AND no Consume* drain happens for AlwaysFaceYaw (it is a held button, not an edge press).
- Given a sibling stub with `CurrentTargetId = newId`, when `OnInput` runs, then `gi.TargetId == newId`.
- Given missing `IInputSource`, when `OnInput` runs, then a default `GameplayInput` is set (no exception).

### `FusionVerticalSliceSmokeTests.cs` (PlayMode, full Fusion)
- Setup: `var runner = new GameObject("TestRunner").AddComponent<NetworkRunner>(); runner.ProvideInput = true; await runner.StartGame(new StartGameArgs { GameMode = GameMode.Single, Scene = ... });`
- Given a started runner, when `runner.Spawn(playerPrefab, ...)` and `runner.Spawn(dummyPrefab, ...)`, then both `NetworkObject`s have `IsValid == true` and have `Health` siblings with `NetworkedHealth == 100`.
- Given the spawned dummy, when invoking `dummy.GetComponent<Health>().DealDamageRpc(100)` from the same (state authority) peer, then within `Mathf.CeilToInt(0.5 * runner.TickRate)` ticks `IsDead == true`.
- Given a dead dummy with `RespawnAtTick > 0`, when ticking forward at least `Mathf.CeilToInt(RespawnDelaySeconds * runner.TickRate)` ticks, then `IsDead == false`, `NetworkedHealth == StartingHealth`, and `transform.position` equals `SpawnPosition`.
- Given a player and dummy in close range, when constructing a `GameplayInput` with `TargetId = dummy.Id`, `Buttons.SetDown(Spell1)`, and feeding it via `runner.SetPlayerObject` + a manual `OnInput`, then within ~1 tick `dummy.NetworkedHealth` decreases by `SpellRegistry.Get(1).Damage` (Fireball is cast-time today, so adjust to Spell2/Arcane Shot for instant verification).
- Given `LogAssert.NoUnexpectedReceived()`, when the test ends, then no `LogType.Error` / `LogType.Exception` were emitted.
- Teardown: shut down runner, destroy auto-created `TargetingSystem` and `TargetHighlight` GameObjects.

---

## F. Testability gaps and smallest safe seams (proposed only — do NOT implement now)

These seams should be reviewed and approved before any test-driven edits to production code.

1. **seam-1 — pure `CombatValidator` overload.** [`CombatValidator.TryValidate`](../Assets/Scripts/Combat/CombatValidator.cs) couples to `NetworkRunner` only because of `runner.TryFindObject`. Smallest change: keep the existing method, add a sibling overload that takes a resolved `(Transform targetTransform, Health targetHealth)` and runs all the other rules. Production unchanged; tests can hit the overload directly.
2. **seam-2 — internal `SecsToTicks`.** [`NetworkCombatController.SecsToTicks`](../Assets/Scripts/Combat/NetworkCombatController.cs) is private. Smallest change: make it `internal static int SecsToTicks(int tickRate, float seconds)` and have the instance call forward. Add `[assembly: InternalsVisibleTo("Forbes.Tests.EditMode")]` (placed in a tiny `Assets/Scripts/AssemblyInfo.cs`).
3. **seam-3 — `FusionInputProvider.WriteInputForTest(IInputSource, TargetingController, out GameplayInput)`.** [`FusionInputProvider.OnInput`](../Assets/Scripts/Networking/FusionInputProvider.cs) currently writes to a Fusion `NetworkInput`. Add an `internal` helper that builds the `GameplayInput` from injected dependencies; `OnInput` calls it and forwards. Tests skip the runner.
4. **seam-4 — `TargetingController.EnumerateTargetables()` virtual.** Tab cycle uses `Object.FindObjectsByType<Targetable>`. A `protected internal virtual IEnumerable<Targetable> EnumerateTargetables()` lets tests inject a fixed list and assert sort + skip-local + skip-dead logic without scenes.
5. **seam-5 — `Targetable` test stub.** `RequireComponent(typeof(NetworkObject))` blocks unit construction. For tests, compose a real `NetworkObject` on a disabled GameObject (no runner) and assert `DisplayName` only — no seam needed if we accept this small cost.
6. **seam-6 — `KeyboardInputSource` is already abstracted via `IInputSource`.** No seam needed: write a `FakeInputSource` test double, do not test the real `Update` path. Manual-only verification covers `Keyboard.current` reads.
7. **seam-7 — `TrainingDummySpawner` bypass.** Editor-only and coroutine-driven. Smoke tests should NOT rely on it; they should `runner.Spawn(dummyPrefab, ...)` themselves.
8. **seam-8 — auto-bootstrap teardown.** [`TargetingController.AutoCreate`](../Assets/Scripts/Combat/TargetingController.cs) and [`ThirdPersonOrbitCamera.AutoAddToMainCamera`](../Assets/Scripts/Player/ThirdPersonOrbitCamera.cs) fire on `RuntimeInitializeOnLoadMethod`. PlayMode tests should clean up in `[TearDown]`: `Object.DestroyImmediate(GameObject.Find("[TargetingSystem]"))`, etc. No seam needed.
9. ~~**seam-9 — legacy `PlayerCombat`.**~~ **Resolved.** `PlayerCombat.cs` was not on `PlayerCharacter.prefab` and has been deleted from the repo (see `docs/CODE_CLEANUP_AUDIT.md` Finding 3). No seam or guard test needed.

Folder layout (now implemented — both `EditMode/` and `PlayMode/` assemblies exist):

```
Assets/Tests/
├── Common/
│   └── FakeInputSource.cs   (planned)
├── EditMode/
│   ├── Forbes.Tests.EditMode.asmdef
│   └── ...
└── PlayMode/   (planned — optional future PlayMode assembly and fixtures)
```

---

## G. CLI commands (Windows; documentation only — verify locally before automating)

Resolve a Unity executable and run from the project root. The Unity install path is unknown to this plan; pick one of:

```cmd
:: Option A — set once per shell, then reuse
set UNITY_EXE="C:\Program Files\Unity\Hub\Editor\<your-version>\Editor\Unity.exe"

:: Option B — discover via Unity Hub (PowerShell)
$hub = Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$env:UNITY_EXE = "$($hub.FullName)\Editor\Unity.exe"
```

Run EditMode tests (no graphics needed):

```cmd
%UNITY_EXE% ^
  -batchmode -nographics ^
  -projectPath "%CD%" ^
  -runTests -testPlatform editmode ^
  -testResults "%CD%\TestResults\editmode.xml" ^
  -logFile -
```

If you add **PlayMode** tests later (not in this repo now), use batchmode **without** `-nographics` and e.g. `-testPlatform playmode`. See Unity Test Framework docs for `-assemblyNames`.

Notes:
- Exit code is non-zero on test failures; CI can gate on `%ERRORLEVEL%`.
- Add `-testFilter "Forbes.*"` once tests have a stable namespace to avoid running Fusion's own tests.
- Add `-testCategory "Smoke"` for the Fusion smoke pass; tag those tests with `[Category("Smoke")]`.

---

## H. Agent rules (for future AI changes — also pin into AGENTS.md when adopted)

1. **Read this plan first.** Before modifying anything in `Assets/Scripts/Combat`, `Assets/Scripts/Player`, `Assets/Scripts/Networking`, or `Assets/Scripts/Core`, open `docs/TEST_COVERAGE_PLAN.md` and identify which sections cover the change.
2. **Run the relevant tests after the change.** EditMode tests are cheap — always run them. If PlayMode tests exist in the repo, run them after changes to [Health](../Assets/Scripts/Player/Health.cs), [NetworkCombatController](../Assets/Scripts/Combat/NetworkCombatController.cs), [TargetingController](../Assets/Scripts/Combat/TargetingController.cs), [PlayerMovement](../Assets/Scripts/Player/PlayerMovement.cs), or related prefabs.
3. **Do not declare a task complete on a red bar.** A failing test is a regression unless the same change explicitly updates the test and its rationale in this document.
4. **Tests pin existing behaviour.** They are not the spec — they are the safety net for the spec in `AGENTS.md` / `CLAUDE.md` / `docs/architecture.md`. If a test forces a gameplay change to pass, fix the test, not the gameplay.
5. **Do not refactor broad systems while adding tests.** Tests and refactors are separate PRs/commits. The seams in section F are the only edits authorised by this plan, and only after explicit approval.
6. **Constants are load-bearing.** Changing `SpellRegistry` entries, `Health.StartingHealth`, `Health.RespawnDelaySeconds`, or `NetworkCombatController.GcdSec` requires updating tests in the same change and noting the impact in the PR.
7. **Networked enum byte values are forever.** `CombatFailReason` numbering must not change; add new values at the end.
8. **Auto-bootstrapped objects must be torn down.** Any **PlayMode** test (if present) that involves [`TargetingController.AutoCreate`](../Assets/Scripts/Combat/TargetingController.cs) or [`ThirdPersonOrbitCamera.AutoAddToMainCamera`](../Assets/Scripts/Player/ThirdPersonOrbitCamera.cs) must clean up the resulting GameObjects in `[TearDown]`.
