// PlayerInputProvider.cs
// Task #3 — Player Movement (input collection side)
// AC: Input struct populated each tick from keyboard; sent to host via Fusion.

using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForbesPrototype.Player
{
    /// <summary>
    /// Collects raw keyboard input and fills NetworkInputData each tick.
    /// Attach to the same GameObject as NetworkBootstrap, or register separately.
    /// </summary>
    public class PlayerInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData
            {
                Move = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical")),
                CastSpell = Input.GetKey(KeyCode.Q),
                TabTarget = Input.GetKeyDown(KeyCode.Tab),
            };
            input.Set(data);
        }

        // ── Unused callbacks ─────────────────────────────────────────────────
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
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
