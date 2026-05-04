using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  internal enum FusionPlayModeSpellPulse {
    None,
    Spell1,
    Spell2,
    Spell3,
  }

  /// <summary>
  /// Minimal <see cref="INetworkRunnerCallbacks"/> for PlayMode Fusion sessions without a keyboard.
  /// Emits single-tick spell button edges plus a sticky <see cref="GameplayInput.TargetId"/>.
  /// </summary>
  internal sealed class FusionPlayModeTestInputRelay : MonoBehaviour, INetworkRunnerCallbacks {
    NetworkRunner _runner;

    internal NetworkId TargetNetworkId;

    internal FusionPlayModeSpellPulse PendingPulse = FusionPlayModeSpellPulse.None;

    /// <summary>
    /// Held movement vector injected into every tick's <see cref="GameplayInput.Move"/>
    /// until the test resets it to <see cref="Vector2.zero"/>.
    /// Use this to simulate a player walking during a cast or missile flight.
    /// </summary>
    internal Vector2 StickyMove = Vector2.zero;

    void Awake() {
      _runner = GetComponent<NetworkRunner>();
      if (_runner != null) {
        _runner.AddCallbacks(this);
      }
    }

    void OnDestroy() {
      if (_runner != null) {
        _runner.RemoveCallbacks(this);
      }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) {
      var gi = new GameplayInput {
        Move      = StickyMove,
        LookYaw   = 0f,
        TargetId  = TargetNetworkId,
      };

      switch (PendingPulse) {
        case FusionPlayModeSpellPulse.Spell1:
          gi.Buttons.SetDown((int)GameplayButtons.Spell1);
          PendingPulse = FusionPlayModeSpellPulse.None;
          break;
        case FusionPlayModeSpellPulse.Spell2:
          gi.Buttons.SetDown((int)GameplayButtons.Spell2);
          PendingPulse = FusionPlayModeSpellPulse.None;
          break;
        case FusionPlayModeSpellPulse.Spell3:
          gi.Buttons.SetDown((int)GameplayButtons.Spell3);
          PendingPulse = FusionPlayModeSpellPulse.None;
          break;
      }

      input.Set(gi);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
      input.Set(new GameplayInput());
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

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

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
  }
}
