using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Spawns the local player prefab when this client joins the session (Fusion Shared Mode basics tutorial).
/// Lives on the same GameObject as <see cref="NetworkRunner"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks {
  [SerializeField] GameObject PlayerPrefab;

  [Tooltip("Editor / solo test: static target with Health (no input). Spawned once after local player joins.")]
  [SerializeField] GameObject TrainingDummyPrefab;

  [SerializeField] bool spawnTrainingDummyInEditor = true;

  [SerializeField] float trainingDummySpawnOffsetX = 4f;

  NetworkRunner _runner;
  bool _spawnedTrainingDummy;
  bool _trainingDummySpawnStarted;

  void Awake() {
    _runner = GetComponent<NetworkRunner>();
    if (_runner != null) {
      _runner.AddCallbacks(this);
    }

    if (GetComponent<CombatHud>() == null) {
      gameObject.AddComponent<CombatHud>();
    }
  }

  void OnDestroy() {
    if (_runner != null) {
      _runner.RemoveCallbacks(this);
    }
  }

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
    if (player == runner.LocalPlayer && PlayerPrefab != null) {
      var spawned = runner.Spawn(PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity, player);
      if (spawned != null) {
        runner.SetPlayerObject(player, spawned);
      }
    }

    if (player == runner.LocalPlayer && Application.isEditor && spawnTrainingDummyInEditor && TrainingDummyPrefab != null &&
        !_trainingDummySpawnStarted) {
      _trainingDummySpawnStarted = true;
      StartCoroutine(CoSpawnTrainingDummy(runner));
    }
  }

  public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

  public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

  public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

  public void OnInput(NetworkRunner runner, NetworkInput input) {
    var gi = new GameplayInput();

#if ENABLE_INPUT_SYSTEM
    var kb = Keyboard.current;
    if (kb != null) {
      float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
      float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
      gi.Move = new Vector2(x, y);

      if (kb.spaceKey.wasPressedThisFrame) {
        gi.Buttons.SetDown((int)GameplayButtons.Jump);
      }

      if (kb.tabKey.wasPressedThisFrame) {
        gi.Buttons.SetDown((int)GameplayButtons.TabTarget);
      }

      if (kb.digit1Key.wasPressedThisFrame) {
        gi.Buttons.SetDown((int)GameplayButtons.SpellPrimary);
      }

      if (kb.eKey.wasPressedThisFrame) {
        gi.Buttons.SetDown((int)GameplayButtons.RandomizeColor);
      }
    }
#else
    gi.Move = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    if (Input.GetButtonDown("Jump")) {
      gi.Buttons.SetDown((int)GameplayButtons.Jump);
    }

    if (Input.GetKeyDown(KeyCode.Tab)) {
      gi.Buttons.SetDown((int)GameplayButtons.TabTarget);
    }

    if (Input.GetKeyDown(KeyCode.Alpha1)) {
      gi.Buttons.SetDown((int)GameplayButtons.SpellPrimary);
    }

    if (Input.GetKeyDown(KeyCode.E)) {
      gi.Buttons.SetDown((int)GameplayButtons.RandomizeColor);
    }
#endif

    input.Set(gi);
  }

  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
    input.Set(new GameplayInput());
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
    _spawnedTrainingDummy = false;
    _trainingDummySpawnStarted = false;
  }

  /// <summary>
  /// Spawns after a short delay so Shared Mode session is ready (master checks were unreliable here).
  /// </summary>
  IEnumerator CoSpawnTrainingDummy(NetworkRunner runner) {
    yield return null;
    yield return null;
    if (!Application.isEditor || !spawnTrainingDummyInEditor || TrainingDummyPrefab == null || _spawnedTrainingDummy) {
      yield break;
    }

    if (runner == null || !runner.IsRunning) {
      yield break;
    }

    var pos = new Vector3(trainingDummySpawnOffsetX, 1f, 0f);
    var spawnedDummy = runner.Spawn(TrainingDummyPrefab, pos, Quaternion.identity, PlayerRef.None);
    if (spawnedDummy != null) {
      _spawnedTrainingDummy = true;
    } else {
      Debug.LogWarning("PlayerSpawner: TrainingDummy spawn returned null. Check FusionPrefab label on TrainingDummy.prefab and NetworkProjectConfig.");
    }
  }

  public void OnConnectedToServer(NetworkRunner runner) { }

  public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

  public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

  public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

  public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

  public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

  public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

  public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

  public void OnSceneLoadDone(NetworkRunner runner) { }

  public void OnSceneLoadStart(NetworkRunner runner) { }

  public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
}
