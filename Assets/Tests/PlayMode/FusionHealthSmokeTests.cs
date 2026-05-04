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
    NetworkObject                 _player;
    NetworkObject                 _dummy;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _player  = null;
      _dummy   = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_TrainingDummy_LethalDamage_RespawnsAtSpawnPosition() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        Assert.IsNotNull(runner);
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, -4f);

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
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        playerHealth.AuthorityResetNetworkedHealthToStartingForTests();

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => _dummy.IsValid &&
               Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth),
          480,
          messageOnFail: "Dummy HP: " +
          $"valid={_dummy.IsValid} hp={dummyHealth.NetworkedHealth} start={dummyHealth.StartingHealth} " +
          $"dead={dummyHealth.IsDead} sa={dummyHealth.HasStateAuthority}");

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => _player.IsValid &&
               Mathf.Approximately(playerHealth.NetworkedHealth, playerHealth.StartingHealth),
          480,
          messageOnFail: "Player HP: " +
          $"valid={_player.IsValid} hp={playerHealth.NetworkedHealth} start={playerHealth.StartingHealth} " +
          $"dead={playerHealth.IsDead} sa={playerHealth.HasStateAuthority}");

        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f,
          "Dummy should start at StartingHealth once spawned with authority.");
        Assert.IsFalse(dummyHealth.IsDead);

        float lethal = dummyHealth.StartingHealth + 25f;
        dummyHealth.DealDamageRpc(lethal);

        yield return FusionPlayModeTestHelpers.WaitUntil(() => dummyHealth.IsDead, 480);

        Assert.IsTrue(dummyHealth.IsDead);
        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 1e-3f);

        int respawnDue = dummyHealth.RespawnAtTick;
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => !dummyHealth.IsDead,
          1200,
          messageOnFail: $"Respawn timeout: IsDead={dummyHealth.IsDead} hp={dummyHealth.NetworkedHealth} " +
          $"respawnAt={respawnDue} tick~={(int)runner.Tick} ticksExec={runner.TicksExecuted} sa={dummyHealth.HasStateAuthority}");

        Assert.IsFalse(dummyHealth.IsDead);
        Assert.AreEqual(dummyHealth.StartingHealth, dummyHealth.NetworkedHealth, 1e-3f);

        yield return FusionPlayModeTestHelpers.WaitFrames(48);

        float snapTol = 0.28f;
        Assert.LessOrEqual(Vector3.Distance(_dummy.transform.position, dummyHealth.SpawnPosition), snapTol,
          "Respawn should snap the dummy back to networked SpawnPosition.");

        // SpawnPosition XZ must remain close to the intended spawn location.
        // Regression for NT default-state corruption: Fusion can apply an internal (0,0,0)
        // snapshot at tick+1, overriding SpawnPosition to world origin, which makes the mob
        // wander around origin and collide with players there.
        float spawnXzTol = 0.5f;
        float spawnXzDist = Mathf.Sqrt(
          Mathf.Pow(dummyHealth.SpawnPosition.x - spawnDummy.x, 2f) +
          Mathf.Pow(dummyHealth.SpawnPosition.z - spawnDummy.z, 2f));
        Assert.LessOrEqual(spawnXzDist, spawnXzTol,
          $"SpawnPosition XZ should match intended spawn location {spawnDummy}; " +
          $"got {dummyHealth.SpawnPosition}. NT default-state may have corrupted it.");
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_DeadEntity_CharacterController_DisabledDuringDeath_ReenabledOnRespawn() {
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner    = _session.Runner;
        var spawnPos  = new Vector3(6f, 0f, -4f);
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnPos, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent<NetworkMobBrain>(out var mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return null;

        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));
        Assert.IsTrue(_dummy.TryGetComponent(out CharacterController cc),
          "Training dummy must have a CharacterController.");

        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth), 480);

        Assert.IsTrue(cc.enabled,
          "CharacterController must be enabled while entity is alive.");

        // Kill the entity.
        dummyHealth.DealDamageRpc(dummyHealth.StartingHealth + 25f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => dummyHealth.IsDead, 480,
          messageOnFail: "Entity did not die after lethal DealDamageRpc.");

        yield return FusionPlayModeTestHelpers.WaitFrames(4);

        // CharacterController must be disabled on death:
        // an active CC on a dead entity gets pushed by nearby CCs (causing visible jumps)
        // and registers targeting raycasts on an invisible corpse.
        Assert.IsFalse(cc.enabled,
          "CharacterController must be disabled while entity is dead.");

        // Wait for automatic respawn.
        yield return FusionPlayModeTestHelpers.WaitUntil(() => !dummyHealth.IsDead, 1200,
          messageOnFail: $"Respawn timeout: IsDead={dummyHealth.IsDead} hp={dummyHealth.NetworkedHealth} " +
          $"respawnAt={dummyHealth.RespawnAtTick} tick={(int)runner.Tick} sa={dummyHealth.HasStateAuthority}");

        yield return FusionPlayModeTestHelpers.WaitFrames(4);

        Assert.IsTrue(cc.enabled,
          "CharacterController must be re-enabled after respawn.");
      }
    }
  }
}
