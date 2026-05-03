using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion single-player: State Authority mob melee calls <see cref="Health.DealDamageRpc"/> on another
  /// <see cref="Health"/> in range. Two training dummies — victim is pinned (<see cref="FusionPlayModeTestHelpers.PinMobBrainNoCombat"/>).
  /// </summary>
  [TestFixture]
  public class NetworkMobBrainMeleeSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject                   _victim;
    NetworkObject                   _mob;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _victim = null;
      _mob = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_MobBrain_InMeleeRange_DamagesVictim_NotSelf() {
      GameObject dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkRunner runner = _session.Runner;
        Assert.IsNotNull(runner);
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        // Spawn far apart first: mob melee (range 12) would kill the victim before HP settles, and then
        // AuthorityApplyStartingHealthIfUnset skips while IsDead && RespawnAtTick > 0.
        var spawnMob        = new Vector3(40f, 0f, 0f);
        var spawnVictimFar  = new Vector3(80f, 0f, 0f);
        var meleeVictim     = new Vector3(43f, 0f, 0f);
        var spawnFlags      = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner,
          dummyPrefab,
          spawnMob,
          Quaternion.identity,
          PlayerRef.None,
          spawnFlags,
          o => {
            _mob = o;
            ConfigureMobBrainImmediate(_mob.GetComponent<NetworkMobBrain>());
          });

        if (_mob.TryGetComponent<NetworkTransform>(out var mobNt0)) {
          mobNt0.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnVictimFar, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            _victim = o;
            FusionPlayModeTestHelpers.PinMobBrainNoCombat(_victim.GetComponent<NetworkMobBrain>());
          });

        if (_victim.TryGetComponent<NetworkTransform>(out var victimNt)) {
          victimNt.DisableSharedModeInterpolation = true;
        }

        var brain = _mob.GetComponent<NetworkMobBrain>();
        Assert.IsNotNull(brain);

        yield return FusionPlayModeTestHelpers.WaitFrames(6);
        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_mob, spawnMob);
        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_victim, spawnVictimFar);
        brain.RefreshWanderOriginAuthority();
        if (_victim.TryGetComponent<NetworkMobBrain>(out var victimBrainForOrigin)) {
          victimBrainForOrigin.RefreshWanderOriginAuthority();
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(4);

        Assert.IsTrue(brain.HasStateAuthority,
          "Mob must simulate on State Authority for tick melee.");

        Assert.IsTrue(_victim.TryGetComponent(out Health victimHealth));
        Assert.IsTrue(_mob.TryGetComponent(out Health mobHealth));
        mobHealth.AuthorityApplyStartingHealthIfUnset();
        victimHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        const float hpTol = 1.5f;
        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => _victim.IsValid &&
               _mob.IsValid &&
               victimHealth.Object != null &&
               victimHealth.Object.IsValid &&
               mobHealth.Object != null &&
               mobHealth.Object.IsValid &&
               Mathf.Abs(victimHealth.NetworkedHealth - victimHealth.StartingHealth) <= hpTol &&
               Mathf.Abs(mobHealth.NetworkedHealth - mobHealth.StartingHealth) <= hpTol,
          1200,
          () =>
            $"HP not settled (tol={hpTol}). " +
            $"victimHP={victimHealth.NetworkedHealth} / {victimHealth.StartingHealth} sa={victimHealth.HasStateAuthority} " +
            $"dead={victimHealth.IsDead} respawnAt={victimHealth.RespawnAtTick} " +
            $"mobHP={mobHealth.NetworkedHealth} / {mobHealth.StartingHealth} sa={mobHealth.HasStateAuthority}");

        // Snapshot HP before closing distance (mob attacks as soon as victim is in range).
        float victimHpBeforeMelee = victimHealth.NetworkedHealth;
        float mobHpBeforeMelee    = mobHealth.NetworkedHealth;
        Assert.GreaterOrEqual(victimHpBeforeMelee, victimHealth.StartingHealth - hpTol,
          "Victim should be at full HP before entering mob attack range.");
        Assert.GreaterOrEqual(mobHpBeforeMelee, mobHealth.StartingHealth - hpTol);

        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_victim, meleeVictim);
        brain.RefreshWanderOriginAuthority();
        if (_victim.TryGetComponent<NetworkMobBrain>(out var victimBrainAfterMeleeMove)) {
          victimBrainAfterMeleeMove.RefreshWanderOriginAuthority();
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(4);

        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => _victim.IsValid &&
               _mob.IsValid &&
               NetworkMobBrainLogic.IsWithinHorizontalRange(
                 _mob.transform.position,
                 _victim.transform.position,
                 brain.AttackRange),
          480,
          () =>
            $"Expected mob/victim within melee (range={brain.AttackRange}). " +
            $"mob={_mob.transform.position} victim={_victim.transform.position} " +
            $"hzDist={Mathf.Sqrt(NetworkMobBrainLogic.HorizontalSqrDistance(_mob.transform.position, _victim.transform.position)):F3}");

        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => _victim.IsValid && victimHealth.NetworkedHealth < victimHpBeforeMelee - 0.5f,
          900,
          () =>
            $"Victim HP did not drop (beforeMelee={victimHpBeforeMelee}, now={victimHealth.NetworkedHealth}, dead={victimHealth.IsDead}). " +
            $"mobAuth={brain.HasStateAuthority} inRange=" +
            $"{NetworkMobBrainLogic.IsWithinHorizontalRange(_mob.transform.position, _victim.transform.position, brain.AttackRange)}");

        Assert.Less(victimHealth.NetworkedHealth, victimHpBeforeMelee);
        Assert.AreEqual(mobHpBeforeMelee, mobHealth.NetworkedHealth, 1e-2f,
          "Mob should not damage its own Health.");

        victimHealth.DealDamageRpc(victimHealth.StartingHealth + 50f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => victimHealth.IsDead, 480);
        float hpWhenDead = victimHealth.NetworkedHealth;
        yield return FusionPlayModeTestHelpers.WaitFrames(90);
        Assert.AreEqual(hpWhenDead, victimHealth.NetworkedHealth, 1e-3f);
        Assert.AreEqual(mobHpBeforeMelee, mobHealth.NetworkedHealth, 1e-2f,
          "Mob HP should be unchanged after victim lethal (no self-damage).");
      }
    }

    static void ConfigureMobBrainImmediate(NetworkMobBrain brain) {
      if (brain == null) {
        return;
      }

      brain.WanderRadius          = 0f;
      brain.WalkSpeed             = 0f;
      brain.RunSpeed              = 0f;
      brain.MinLegDistance        = 0f;
      brain.IdleTicksMin          = 1;
      brain.IdleTicksMax          = 1;
      brain.AttackRange           = 12f;
      brain.AggroRadius           = 12f;
      brain.AttackDamage          = 6f;
      brain.AttackIntervalSeconds = 0.05f;
    }
  }
}
