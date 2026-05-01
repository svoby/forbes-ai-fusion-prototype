// NetworkBootstrap.cs
// Task #1 — Project Setup & Fusion Bootstrap
// AC: First peer becomes host; second peer joins as client.
// AC: Console logs "Connected as Host" / "Connected as Client".

using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForbesPrototype.Networking
{
    /// <summary>
    /// Starts the Fusion runner in AutoHostOrClient mode on scene load.
    /// Implements INetworkRunnerCallbacks to forward player-join events to SpawnManager.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _runnerPrefab;
        [SerializeField] private SpawnManager _spawnManager;

        private NetworkRunner _runner;

        private async void Start()
        {
            _runner = Instantiate(_runnerPrefab);
            _runner.AddCallbacks(this);

            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "ForbesPrototype",
                Scene = SceneRef.FromIndex(0),
            });

            if (result.Ok)
                Debug.Log(_runner.IsServer ? "Connected as Host" : "Connected as Client");
            else
                Debug.LogError($"Connection failed: {result.ShutdownReason}");
        }

        // ── INetworkRunnerCallbacks ──────────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
            => _spawnManager.OnPlayerJoined(runner, player);

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
            => _spawnManager.OnPlayerLeft(runner, player);

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessage message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
