using System;
using System.Collections;
using Fusion;
using Fusion.Sockets;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Minimal Fusion <see cref="GameMode.Single"/> session for PlayMode tests.
  /// </summary>
  internal sealed class FusionSinglePlayerTestSession {
    GameObject _host;
    NetworkRunner _runner;

    internal NetworkRunner Runner => _runner;

    internal IEnumerator Start() {
      Assert.IsNull(_host, "Session already started.");

      _host = new GameObject(nameof(FusionSinglePlayerTestSession) + "_RunnerHost");
      _runner = _host.AddComponent<NetworkRunner>();

      if (_host.GetComponent<NetworkSceneManagerDefault>() == null) {
        _host.AddComponent<NetworkSceneManagerDefault>();
      }

      if (_host.GetComponent<NetworkObjectProviderDefault>() == null) {
        _host.AddComponent<NetworkObjectProviderDefault>();
      }

      var sceneManager = _host.GetComponent<NetworkSceneManagerDefault>();
      var objectProvider = _host.GetComponent<NetworkObjectProviderDefault>();
      // Global config uses PeerModes.Multiple; without Fusion's normal scene setup,
      // SceneManager stays "busy" (no multi-peer roots). The default provider would then
      // return Retry forever and spawns stay Queued — PlayMode tests would hang/time out.
      objectProvider.DelayIfSceneManagerIsBusy = false;

      var startTask = _runner.StartGame(new StartGameArgs {
        GameMode = GameMode.Single,
        SessionName = Guid.NewGuid().ToString("N"),
        Address = NetAddress.Any(0),
        SceneManager = sceneManager,
        ObjectProvider = objectProvider,
      });

      yield return new WaitUntil(() => startTask.IsCompleted);

      Assert.IsFalse(startTask.IsFaulted, startTask.Exception?.ToString());
      Assert.IsTrue(startTask.Result.Ok, startTask.Result.ErrorMessage);
      Assert.IsTrue(_runner.IsRunning, "Runner should be running after StartGame.");
    }

    internal IEnumerator ShutdownAndDestroy() {
      if (_runner != null && _runner.IsRunning) {
        _runner.Shutdown(false, ShutdownReason.Ok, true);
      }

      var safety = 0;
      while (_runner != null && _runner.IsRunning && safety < 600) {
        safety++;
        yield return null;
      }

      if (_host != null) {
        UnityEngine.Object.DestroyImmediate(_host);
        _host = null;
      }

      _runner = null;
    }
  }
}
