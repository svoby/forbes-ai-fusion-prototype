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

  [Tooltip("Offset in the local player's space (X=right, Y=up, Z=forward) where the dummy is spawned.")]
  [SerializeField] Vector3 trainingDummySpawnOffsetLocal = new Vector3(1.5f, 0f, 3.5f);

  NetworkRunner _runner;
  bool _spawnedTrainingDummy;
  bool _trainingDummySpawnStarted;

  // Fusion calls OnInput on a different timing than Unity's wasPressedThisFrame; latch edges in Update (Photon manual).
  bool _pendingJump;
  bool _pendingTab;
  bool _pendingSpell;
  bool _pendingColor;

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

  void Update() {
#if ENABLE_INPUT_SYSTEM
    var kb = Keyboard.current;
    if (kb == null) {
      return;
    }

    if (kb.spaceKey.wasPressedThisFrame) {
      _pendingJump = true;
    }

    if (kb.tabKey.wasPressedThisFrame) {
      _pendingTab = true;
    }

    if (kb.digit1Key.wasPressedThisFrame) {
      _pendingSpell = true;
    }

    if (kb.eKey.wasPressedThisFrame) {
      _pendingColor = true;
    }
#else
    if (Input.GetButtonDown("Jump")) {
      _pendingJump = true;
    }

    if (Input.GetKeyDown(KeyCode.Tab)) {
      _pendingTab = true;
    }

    if (Input.GetKeyDown(KeyCode.Alpha1)) {
      _pendingSpell = true;
    }

    if (Input.GetKeyDown(KeyCode.E)) {
      _pendingColor = true;
    }
#endif
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

      if (_pendingJump) {
        gi.Buttons.SetDown((int)GameplayButtons.Jump);
        _pendingJump = false;
      }

      if (_pendingTab) {
        gi.Buttons.SetDown((int)GameplayButtons.TabTarget);
        _pendingTab = false;
      }

      if (_pendingSpell) {
        gi.Buttons.SetDown((int)GameplayButtons.SpellPrimary);
        _pendingSpell = false;
      }

      if (_pendingColor) {
        gi.Buttons.SetDown((int)GameplayButtons.RandomizeColor);
        _pendingColor = false;
      }
    }
#else
    gi.Move = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    if (_pendingJump) {
      gi.Buttons.SetDown((int)GameplayButtons.Jump);
      _pendingJump = false;
    }

    if (_pendingTab) {
      gi.Buttons.SetDown((int)GameplayButtons.TabTarget);
      _pendingTab = false;
    }

    if (_pendingSpell) {
      gi.Buttons.SetDown((int)GameplayButtons.SpellPrimary);
      _pendingSpell = false;
    }

    if (_pendingColor) {
      gi.Buttons.SetDown((int)GameplayButtons.RandomizeColor);
      _pendingColor = false;
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
  /// Waits for <see cref="NetworkRunner.TryGetPlayerObject"/> so spawn does not fail with local player not set yet,
  /// then spawns in front of the player so the dummy stays in view.
  /// </summary>
  IEnumerator CoSpawnTrainingDummy(NetworkRunner runner) {
    if (TrainingDummyPrefab.GetComponent<NetworkObject>() == null) {
      Debug.LogError("PlayerSpawner: TrainingDummyPrefab must be a root object with a NetworkObject.");
      _trainingDummySpawnStarted = false;
      yield break;
    }

    float waited = 0f;
    while (waited < 10f && runner != null && runner.IsRunning && !_spawnedTrainingDummy) {
      if (Application.isEditor && spawnTrainingDummyInEditor &&
          runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObj) && playerObj != null) {
        var worldPos = playerObj.transform.TransformPoint(trainingDummySpawnOffsetLocal);
        worldPos.y = Mathf.Max(worldPos.y, 0.5f);
        var rot = playerObj.transform.rotation;

        var spawnedDummy = runner.Spawn(TrainingDummyPrefab, worldPos, rot, PlayerRef.None);
        if (spawnedDummy != null) {
          _spawnedTrainingDummy = true;
          Debug.Log("PlayerSpawner: Training dummy spawned.", spawnedDummy.gameObject);
        } else {
          Debug.LogWarning("PlayerSpawner: Training dummy Spawn returned null (prefab / FusionPrefab / spawn rules).");
          _trainingDummySpawnStarted = false;
        }

        yield break;
      }

      waited += Time.unscaledDeltaTime;
      yield return null;
    }

    Debug.LogWarning("PlayerSpawner: Training dummy not spawned (timeout waiting for local player object).");
    _trainingDummySpawnStarted = false;
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
