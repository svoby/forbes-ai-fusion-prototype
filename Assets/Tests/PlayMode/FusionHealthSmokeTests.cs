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
        yield return WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy = new Vector3(6f, 0f, -4f);

        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        // Health smoke: pin dummy movement/melee (same as victim in mob tests). Do not disable NetworkMobBrain —
        // in Editor PlayMode that can stop sibling simulation ticks (respawn never runs).
        if (_dummy.TryGetComponent<NetworkMobBrain>(out var mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return null;

        Assert.IsTrue(_player.TryGetComponent(out Health playerHealth));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        playerHealth.AuthorityApplyStartingHealthIfUnset();
        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return WaitFrames(5);

        playerHealth.AuthorityResetNetworkedHealthToStartingForTests();

        yield return WaitUntil(
          () => _dummy.IsValid &&
               Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth),
          maxFrames: 480,
          "Dummy HP: " +
          $"valid={_dummy.IsValid} hp={dummyHealth.NetworkedHealth} start={dummyHealth.StartingHealth} " +
          $"dead={dummyHealth.IsDead} sa={dummyHealth.HasStateAuthority}");

        yield return WaitUntil(
          () => _player.IsValid &&
               Mathf.Approximately(playerHealth.NetworkedHealth, playerHealth.StartingHealth),
          maxFrames: 480,
          "Player HP: " +
          $"valid={_player.IsValid} hp={playerHealth.NetworkedHealth} start={playerHealth.StartingHealth} " +
          $"dead={playerHealth.IsDead} sa={playerHealth.HasStateAuthority}");

        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f,
          "Dummy should start at StartingHealth once spawned with authority.");
        Assert.IsFalse(dummyHealth.IsDead);

        float lethal = dummyHealth.StartingHealth + 25f;
        dummyHealth.DealDamageRpc(lethal);

        yield return WaitUntil(() => dummyHealth.IsDead, maxFrames: 480);

        Assert.IsTrue(dummyHealth.IsDead);
        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 1e-3f);

        int respawnDue = dummyHealth.RespawnAtTick;
        yield return WaitUntil(
          () => !dummyHealth.IsDead,
          maxFrames: 1200,
          $"Respawn timeout: IsDead={dummyHealth.IsDead} hp={dummyHealth.NetworkedHealth} " +
          $"respawnAt={respawnDue} tick~={(int)runner.Tick} ticksExec={runner.TicksExecuted} sa={dummyHealth.HasStateAuthority}");

        Assert.IsFalse(dummyHealth.IsDead);
        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f);

        yield return WaitFrames(48);

        float snapTol = 0.28f;
        Assert.LessOrEqual(Vector3.Distance(_dummy.transform.position, dummyHealth.SpawnPosition), snapTol,
          "Respawn should snap the dummy back to networked SpawnPosition.");
      }
    }

    static IEnumerator WaitUntil(Func<bool> predicate, int maxFrames, string messageOnFail = null) {
      int i = 0;
      while (i < maxFrames && !predicate()) {
        i++;
        yield return new WaitForFixedUpdate();
        yield return null;
      }

      if (predicate()) {
        yield break;
      }

      string suffix = !string.IsNullOrEmpty(messageOnFail) ? " " + messageOnFail : "";
      Assert.Fail($"Predicate not satisfied within {maxFrames} frames.{suffix}");
    }

    static IEnumerator WaitFrames(int count) {
      for (var i = 0; i < count; i++) {
        yield return new WaitForFixedUpdate();
        yield return null;
      }
    }
  }
}
