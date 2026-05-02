// Verbose Fusion/editor logs — delete this line (and FORBES_NET_LOG usages compile out).
#define FORBES_NET_LOG

using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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
    NetLog($"OnShutdown reason={shutdownReason}");
    _spawnedTrainingDummy = false;
    _trainingDummySpawnStarted = false;
  }

  /// <summary>
  /// Spawns in front of the local player; falls back to in front of the main camera.
  /// Uses <see cref="NetworkSpawnFlags.SharedModeStateAuthLocalPlayer"/> so Shared Mode accepts spawn from the local peer.
  /// </summary>
  IEnumerator CoSpawnTrainingDummy(NetworkRunner runner) {
    NetLog("CoSpawnTrainingDummy started");

    if (TrainingDummyPrefab.GetComponent<NetworkObject>() == null) {
      Debug.LogError("PlayerSpawner: TrainingDummyPrefab must be a root object with a NetworkObject.");
      _trainingDummySpawnStarted = false;
      yield break;
    }

    var spawnFailures = 0;
    const int maxFrames = 300;
    for (var frame = 0; frame < maxFrames && runner != null && runner.IsRunning && !_spawnedTrainingDummy; frame++) {
      yield return null;

      if (!Application.isEditor || !spawnTrainingDummyInEditor) {
        NetLog("CoSpawnTrainingDummy aborted (not editor or spawn disabled)");
        _trainingDummySpawnStarted = false;
        yield break;
      }

      var havePlayer = runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObj) && playerObj != null;
      if (!havePlayer && frame < 90) {
        if (frame == 0 || frame == 30 || frame == 60) {
          NetLog($"CoSpawnTrainingDummy frame={frame} waiting for local player object…");
        }

        continue;
      }

      Vector3 worldPos;
      Quaternion rot;
      string mode;
      if (havePlayer) {
        worldPos = playerObj.transform.TransformPoint(trainingDummySpawnOffsetLocal);
        worldPos.y = Mathf.Max(worldPos.y, 0.5f);
        rot = playerObj.transform.rotation;
        mode = "player-relative";
      } else if (Camera.main != null) {
        var cam = Camera.main.transform;
        var flatFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 1e-4f) {
          flatFwd = Vector3.forward;
        }

        flatFwd.Normalize();
        worldPos = cam.position + flatFwd * 4f;
        worldPos.y = Mathf.Max(worldPos.y, 1f);
        rot = Quaternion.LookRotation(flatFwd);
        mode = "camera-fallback";
      } else {
        worldPos = new Vector3(4f, 1f, 0f);
        rot = Quaternion.identity;
        mode = "fixed-fallback";
      }

      NetLog($"CoSpawnTrainingDummy frame={frame} mode={mode} pos={worldPos} havePlayer={havePlayer}");

      var spawnedDummy = runner.Spawn(
        TrainingDummyPrefab,
        worldPos,
        rot,
        PlayerRef.None,
        null,
        NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);

      if (spawnedDummy != null) {
        _spawnedTrainingDummy = true;
        NetLog($"Training dummy spawned name={spawnedDummy.name} id={spawnedDummy.Id} at {worldPos}");
        yield break;
      }

      NetLog($"Training dummy Spawn returned null (attempt {spawnFailures + 1})");
      spawnFailures++;
      if (spawnFailures >= 12) {
        Debug.LogError("PlayerSpawner: Training dummy Spawn returned null repeatedly. Check FusionPrefab label on TrainingDummy and Shared Mode spawn rules.");
        _trainingDummySpawnStarted = false;
        yield break;
      }
    }

    Debug.LogWarning("PlayerSpawner: Training dummy not spawned (frame budget exhausted).");
    _trainingDummySpawnStarted = false;
  }

  public void OnConnectedToServer(NetworkRunner runner) {
    NetLog("OnConnectedToServer");
    TryStartEditorTrainingDummySpawn(runner);
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

#if UNITY_EDITOR
    if (TrainingDummyPrefab == null) {
      TrainingDummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummyPrefabAssetPath);
      if (TrainingDummyPrefab != null) {
        EditorUtility.SetDirty(this);
        NetLog($"TryStartDummy: loaded TrainingDummyPrefab from {TrainingDummyPrefabAssetPath}");
      }
    }
#endif

    if (TrainingDummyPrefab == null) {
      NetLog("TryStartDummy skip: TrainingDummyPrefab is NULL (assign in scene or keep Assets/TrainingDummy.prefab)");
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
