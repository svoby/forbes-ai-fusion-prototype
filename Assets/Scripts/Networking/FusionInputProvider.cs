using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Bridges a sibling <see cref="IInputSource"/> (and an optional sibling
/// <see cref="TargetingController"/>) to Fusion's per-tick input struct.
/// Lives on the same GameObject as <see cref="NetworkRunner"/>.
/// </summary>
[DisallowMultipleComponent]
public class FusionInputProvider : MonoBehaviour, INetworkRunnerCallbacks {
  NetworkRunner       _runner;
  IInputSource        _input;
  TargetingController _targeting; // may be null until M2 adds it

  void Awake() {
    _runner    = GetComponent<NetworkRunner>();
    _input     = GetComponent<IInputSource>();
    _targeting = GetComponent<TargetingController>(); // may also be auto-created after Awake; see OnInput

    if (_runner != null) {
      _runner.AddCallbacks(this);
    }

    if (_input == null) {
      ForbesLog.Warn("FusionInputProvider: no IInputSource sibling found; OnInput will emit empty input.");
    }
  }

  void OnDestroy() {
    if (_runner != null) {
      _runner.RemoveCallbacks(this);
    }
  }

  public void OnInput(NetworkRunner runner, NetworkInput input) {
    // TargetingController may have been auto-created after our Awake ran.
    if (_targeting == null) {
      _targeting = UnityEngine.Object.FindAnyObjectByType<TargetingController>();
    }

    var gi = new GameplayInput();

    if (_input != null) {
      gi.Move    = _input.MoveAxes;
      gi.LookYaw = _input.LookYaw;

      if (_input.AlwaysFaceYaw)       { gi.Buttons.SetDown((int)GameplayButtons.AlwaysFaceYaw); }
      if (_input.ConsumeJump())        { gi.Buttons.SetDown((int)GameplayButtons.Jump); }
      if (_input.ConsumeSpell1())      { gi.Buttons.SetDown((int)GameplayButtons.Spell1); }
      if (_input.ConsumeSpell2())      { gi.Buttons.SetDown((int)GameplayButtons.Spell2); }
      if (_input.ConsumeSpell3())      { gi.Buttons.SetDown((int)GameplayButtons.Spell3); }
      if (_input.ConsumeRandomizeColor()) { gi.Buttons.SetDown((int)GameplayButtons.RandomizeColor); }
    }

    // Include the local target's NetworkId so the state authority can validate it at cast time.
    if (_targeting != null) {
      gi.TargetId = _targeting.CurrentTargetId;
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
