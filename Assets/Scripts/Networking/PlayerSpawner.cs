// Verbose Fusion/editor logs — delete this line (and FORBES_NET_LOG usages compile out).
#define FORBES_NET_LOG

using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns the local player prefab when this client joins the session (Fusion Shared Mode basics tutorial).
/// Lives on the same GameObject as <see cref="NetworkRunner"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks {
#if UNITY_EDITOR
  const string TrainingDummyPrefabAssetPath = "Assets/TrainingDummy.prefab";
#endif

  [System.Diagnostics.Conditional("FORBES_NET_LOG")]
  static void NetLog(string message) {
    UnityEngine.Debug.Log("[ForbesNet] " + message);
  }

  [SerializeField] GameObject PlayerPrefab;

  [Tooltip("Editor / solo test: static target with Health (no input). Spawned once after local player joins.")]
  [SerializeField] GameObject TrainingDummyPrefab;

  [SerializeField] bool spawnTrainingDummyInEditor = true;

  [Tooltip("Offset in the local player's space (X=right, Y=up, Z=forward). Default is straight ahead so FP camera sees the dummy.")]
  [SerializeField] Vector3 trainingDummySpawnOffsetLocal = new Vector3(0f, 0f, 4f);

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

#if UNITY_EDITOR
    if (Application.isEditor && TrainingDummyPrefab == null) {
      TrainingDummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummyPrefabAssetPath);
      if (TrainingDummyPrefab != null) {
        EditorUtility.SetDirty(this);
        NetLog($"Awake: restored TrainingDummyPrefab from {TrainingDummyPrefabAssetPath} (scene reference was missing).");
      }
    }
#endif

    NetLog($"Awake runner={(_runner != null)}, TrainingDummyPrefab={(TrainingDummyPrefab != null ? TrainingDummyPrefab.name : "NULL")}, spawnDummy={spawnTrainingDummyInEditor}");

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
  }

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
    NetLog($"OnPlayerJoined player={player} local={runner.LocalPlayer} running={runner.IsRunning}");

    if (player == runner.LocalPlayer && PlayerPrefab != null) {
      var spawned = runner.Spawn(PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity, player);
      NetLog($"Spawn local player -> {(spawned != null ? spawned.name : "NULL")}");
      if (spawned != null) {
        runner.SetPlayerObject(player, spawned);
        NetLog($"SetPlayerObject local={runner.LocalPlayer} ok={runner.TryGetPlayerObject(runner.LocalPlayer, out _)}");
      }
    }

    TryStartEditorTrainingDummySpawn(runner);
  }

  public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

  public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

  public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

  public void OnInput(NetworkRunner runner, NetworkInput input) {
    var gi = new GameplayInput();

    var kb = Keyboard.current;
    if (kb != null) {
      float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
      float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
      gi.Move = new Vector2(x, y);
    }

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

    input.Set(gi);
  }

  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
    input.Set(new GameplayInput());
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
    NetLog($"OnShutdown reason={shutdownReason}");
    _spawnedTrainingDummy = false;
    _trainingDummySpawnStarted = false;
  }

  /// <summary>
  /// Spawns in front of the local player after <see cref="NetworkRunner.SetPlayerObject"/>.
  /// Uses <see cref="NetworkSpawnFlags.SharedModeStateAuthLocalPlayer"/> so Shared Mode accepts spawn from the local peer.
  /// </summary>
  IEnumerator CoSpawnTrainingDummy(NetworkRunner runner) {
    NetLog("CoSpawnTrainingDummy started");

    if (TrainingDummyPrefab.GetComponent<NetworkObject>() == null) {
      Debug.LogError("PlayerSpawner: TrainingDummyPrefab must be a root object with a NetworkObject.");
      _trainingDummySpawnStarted = false;
      yield break;
    }

    const int maxWaitFrames = 32;
    for (var frame = 0; frame < maxWaitFrames; frame++) {
      yield return null;

      if (!Application.isEditor || !spawnTrainingDummyInEditor || runner == null || !runner.IsRunning) {
        NetLog("CoSpawnTrainingDummy aborted (not editor, disabled, or runner stopped)");
        _trainingDummySpawnStarted = false;
        yield break;
      }

      if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObj) || playerObj == null) {
        continue;
      }

      var worldPos = playerObj.transform.TransformPoint(trainingDummySpawnOffsetLocal);
      worldPos.y = Mathf.Max(worldPos.y, 0.5f);
      var rot = playerObj.transform.rotation;

      NetLog($"CoSpawnTrainingDummy frame={frame} pos={worldPos}");

      var spawnedDummy = runner.Spawn(
        TrainingDummyPrefab,
        worldPos,
        rot,
        PlayerRef.None,
        null,
        NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);

      if (spawnedDummy != null) {
        _spawnedTrainingDummy = true;
        NetLog($"Training dummy spawned name={spawnedDummy.name} id={spawnedDummy.Id}");
        yield break;
      }

      Debug.LogError("PlayerSpawner: Training dummy Spawn returned null. Check FusionPrefab label on TrainingDummy and Shared Mode spawn rules.");
      _trainingDummySpawnStarted = false;
      yield break;
    }

    Debug.LogWarning("PlayerSpawner: Training dummy not spawned (local player object not ready in time).");
    _trainingDummySpawnStarted = false;
  }

  public void OnConnectedToServer(NetworkRunner runner) {
    NetLog("OnConnectedToServer");
  }

  void TryStartEditorTrainingDummySpawn(NetworkRunner runner) {
    if (!Application.isEditor) {
      NetLog("TryStartDummy skip: not editor");
      return;
    }

    if (!spawnTrainingDummyInEditor) {
      NetLog("TryStartDummy skip: spawnTrainingDummyInEditor=false");
      return;
    }

    if (TrainingDummyPrefab == null) {
      NetLog("TryStartDummy skip: TrainingDummyPrefab is NULL (assign in scene or keep Assets/TrainingDummy.prefab)");
      return;
    }

    if (_spawnedTrainingDummy) {
      return;
    }

    if (_trainingDummySpawnStarted) {
      NetLog("TryStartDummy skip: coroutine already started");
      return;
    }

    NetLog("TryStartDummy: starting CoSpawnTrainingDummy");
    _trainingDummySpawnStarted = true;
    StartCoroutine(CoSpawnTrainingDummy(runner));
  }

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
