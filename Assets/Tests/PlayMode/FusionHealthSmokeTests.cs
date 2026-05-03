using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion Single-player smoke: networked HP, death, respawn, spawn snap-back.
  /// </summary>
  [TestFixture]
  public class FusionHealthSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject _player;
    NetworkObject _dummy;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _player = null;
      _dummy = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_TrainingDummy_LethalDamage_RespawnsAtSpawnPosition() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        Assert.IsNotNull(runner);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy = new Vector3(6f, 0f, -4f);

        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        // Health smoke asserts respawn snap: disable wander (Fusion may still tick disabled NB) and shared-mode
        // interpolation so render transform matches authority teleport after Respawn().
        if (_dummy.TryGetComponent<NetworkMobBrain>(out var mobBrain)) {
          UnityEngine.Object.Destroy(mobBrain);
        }

        if (_dummy.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return null;

        Assert.IsTrue(_player.TryGetComponent(out Health playerHealth));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        yield return WaitUntil(
          () => _dummy.IsValid &&
               Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth),
          maxFrames: 480);

        yield return WaitUntil(
          () => _player.IsValid &&
               Mathf.Approximately(playerHealth.NetworkedHealth, playerHealth.StartingHealth),
          maxFrames: 480);

        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f,
          "Dummy should start at StartingHealth once spawned with authority.");
        Assert.IsFalse(dummyHealth.IsDead);

        float lethal = dummyHealth.StartingHealth + 25f;
        dummyHealth.DealDamageRpc(lethal);

        yield return WaitUntil(() => dummyHealth.IsDead, maxFrames: 480);

        Assert.IsTrue(dummyHealth.IsDead);
        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 1e-3f);

        yield return WaitUntil(() => !dummyHealth.IsDead, maxFrames: 1200);

        Assert.IsFalse(dummyHealth.IsDead);
        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f);

        yield return WaitFrames(48);

        float snapTol = 0.28f;
        Assert.LessOrEqual(Vector3.Distance(_dummy.transform.position, dummyHealth.SpawnPosition), snapTol,
          "Respawn should snap the dummy back to networked SpawnPosition.");
      }
    }

    static IEnumerator WaitUntil(Func<bool> predicate, int maxFrames) {
      int i = 0;
      while (i < maxFrames && !predicate()) {
        i++;
        yield return null;
      }

      Assert.IsTrue(predicate(), $"Predicate not satisfied within {maxFrames} frames.");
    }

    static IEnumerator WaitFrames(int count) {
      for (var i = 0; i < count; i++) {
        yield return null;
      }
    }
  }
}
