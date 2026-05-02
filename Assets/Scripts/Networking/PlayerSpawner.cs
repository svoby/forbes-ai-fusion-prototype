using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Single responsibility: when the local player joins the room, spawn the player
/// prefab and register it with the runner. Input goes through
/// <see cref="FusionInputProvider"/>; editor-only training dummy spawning lives in
/// <see cref="TrainingDummySpawner"/>; HUD wiring is the scene's job.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks {
  [SerializeField] GameObject PlayerPrefab;

  /// <summary>Raised on this peer right after the local player object is spawned and registered.</summary>
  public event Action LocalPlayerSpawned;

  NetworkRunner _runner;

  void Awake() {
    _runner = GetComponent<NetworkRunner>();
    if (_runner != null) {
      _runner.AddCallbacks(this);
    }

    ForbesLog.Net($"PlayerSpawner Awake runner={(_runner != null)}");
  }

  void OnDestroy() {
    if (_runner != null) {
      _runner.RemoveCallbacks(this);
    }
  }

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
    ForbesLog.Net($"OnPlayerJoined player={player} local={runner.LocalPlayer} running={runner.IsRunning}");

    if (player != runner.LocalPlayer || PlayerPrefab == null) {
      return;
    }

    var spawned = runner.Spawn(PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity, player);
    ForbesLog.Net($"Spawn local player -> {(spawned != null ? spawned.name : "NULL")}");
    if (spawned == null) {
      return;
    }

    runner.SetPlayerObject(player, spawned);
    ForbesLog.Net($"SetPlayerObject local={runner.LocalPlayer} ok={runner.TryGetPlayerObject(runner.LocalPlayer, out _)}");
    LocalPlayerSpawned?.Invoke();
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
    ForbesLog.Net($"OnShutdown reason={shutdownReason}");
  }

  public void OnConnectedToServer(NetworkRunner runner) {
    ForbesLog.Net("OnConnectedToServer");
  }

  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
  public void OnInput(NetworkRunner runner, NetworkInput input) { }
  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
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
  public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
  public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
  public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
