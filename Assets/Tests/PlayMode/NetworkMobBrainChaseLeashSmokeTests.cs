using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion single-player: aggro → chase → melee and leash → return smoke checks.
  /// </summary>
  [TestFixture]
  public class NetworkMobBrainChaseLeashSmokeTests {
    FusionSinglePlayerTestSession _session;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_Mob_ChasesVictimInsideAggro_FacesChaseDirection() {
      GameObject prefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(prefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkRunner runner = _session.Runner;
        Assert.IsNotNull(runner);

        NetworkObject mob = null;
        NetworkObject victim = null;
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        var spawnMob = new Vector3(120f, 0f, 0f);
        var spawnVictim = new Vector3(127f, 0f, 0f);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, spawnMob, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            mob = o;
            ConfigureMobForChaseScenario(mob.GetComponent<NetworkMobBrain>());
          });

        if (mob.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, spawnVictim, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            victim = o;
            FusionPlayModeTestHelpers.PinMobBrainNoCombat(o.GetComponent<NetworkMobBrain>());
          });

        if (victim.TryGetComponent<NetworkTransform>(out var victimNt)) {
          victimNt.DisableSharedModeInterpolation = true;
        }

        yield return WaitFrames(6);
        AuthorityTeleportTo(mob, spawnMob);
        AuthorityTeleportTo(victim, spawnVictim);
        mob.GetComponent<NetworkMobBrain>().RefreshWanderOriginAuthority();

        yield return WaitFrames(8);

        float d0 = HorizDist(mob.transform.position, victim.transform.position);
        Assert.Greater(d0, 1.6f, "victim should start outside attack range.");

        yield return WaitFrames(20);

        float d1 = HorizDist(mob.transform.position, victim.transform.position);
        Assert.Less(d1, d0 - 0.08f, "mob should close horizontal distance while chasing.");

        Vector3 toVictim = victim.transform.position - mob.transform.position;
        toVictim.y = 0f;
        Vector3 fwd = mob.transform.forward;
        fwd.y = 0f;
        if (toVictim.sqrMagnitude > 1e-6f && fwd.sqrMagnitude > 1e-6f) {
          toVictim.Normalize();
          fwd.Normalize();
          Assert.Greater(
            Vector3.Dot(fwd, toVictim),
            0.72f,
            "mob forward should roughly align with chase direction on XZ.");
        }
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_Mob_AfterChase_ReachesMeleeAndDamagesVictim_NotSelf() {
      GameObject prefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(prefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkRunner runner = _session.Runner;
        NetworkObject mob = null;
        NetworkObject victim = null;
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        var spawnMob = new Vector3(140f, 0f, 0f);
        var spawnVictimFar = new Vector3(180f, 0f, 0f);
        var victimChaseSpot = new Vector3(145.5f, 0f, 0f);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, spawnMob, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            mob = o;
            ConfigureMobForChaseScenario(mob.GetComponent<NetworkMobBrain>());
          });

        if (mob.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, spawnVictimFar, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            victim = o;
            FusionPlayModeTestHelpers.PinMobBrainNoCombat(o.GetComponent<NetworkMobBrain>());
          });

        if (victim.TryGetComponent<NetworkTransform>(out var victimNt)) {
          victimNt.DisableSharedModeInterpolation = true;
        }

        var brain = mob.GetComponent<NetworkMobBrain>();
        var mobHealth = mob.GetComponent<Health>();
        var victimHealth = victim.GetComponent<Health>();

        yield return WaitFrames(6);
        AuthorityTeleportTo(mob, spawnMob);
        AuthorityTeleportTo(victim, spawnVictimFar);
        brain.RefreshWanderOriginAuthority();

        yield return WaitFrames(5);
        mobHealth.AuthorityApplyStartingHealthIfUnset();
        victimHealth.AuthorityApplyStartingHealthIfUnset();

        const float hpTol = 1.5f;
        yield return WaitUntil(
          () => mob.IsValid && victim.IsValid &&
               Mathf.Abs(victimHealth.NetworkedHealth - victimHealth.StartingHealth) <= hpTol &&
               Mathf.Abs(mobHealth.NetworkedHealth - mobHealth.StartingHealth) <= hpTol,
          maxFrames: 1200,
          failureMessage: () => "HP not settled.");

        float victimHpBefore = victimHealth.NetworkedHealth;
        float mobHpBefore = mobHealth.NetworkedHealth;

        AuthorityTeleportTo(victim, victimChaseSpot);
        brain.RefreshWanderOriginAuthority();
        yield return WaitFrames(6);

        yield return WaitUntil(
          () => victim.IsValid && victimHealth.NetworkedHealth < victimHpBefore - 0.5f,
          maxFrames: 1500,
          failureMessage: () =>
            $"Victim HP did not drop (before={victimHpBefore}, now={victimHealth.NetworkedHealth}).");

        victimHealth.DealDamageRpc(victimHealth.StartingHealth + 50f);
        yield return WaitUntil(() => victimHealth.IsDead, maxFrames: 480);
        float hpWhenDead = victimHealth.NetworkedHealth;
        yield return WaitFrames(40);
        Assert.AreEqual(hpWhenDead, victimHealth.NetworkedHealth, 1e-3f);
        Assert.AreEqual(mobHpBefore, mobHealth.NetworkedHealth, 1e-2f, "Mob should not damage itself.");
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_Mob_Leash_ReturnsTowardSpawn_StopsDamagingFarVictim() {
      GameObject prefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(prefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkRunner runner = _session.Runner;
        NetworkObject mob = null;
        NetworkObject victim = null;
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        var spawnMob = new Vector3(200f, 0f, 0f);
        var victimNear = new Vector3(205f, 0f, 0f);
        var victimKited = new Vector3(220f, 0f, 0f);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, spawnMob, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            mob = o;
            var b = o.GetComponent<NetworkMobBrain>();
            ConfigureMobForChaseScenario(b);
            b.LeashRadius = 8f;
            b.AggroRadius = 25f;
          });

        if (mob.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, prefab, victimNear, Quaternion.identity, PlayerRef.None, spawnFlags,
          o => {
            victim = o;
            FusionPlayModeTestHelpers.PinMobBrainNoCombat(o.GetComponent<NetworkMobBrain>());
          });

        if (victim.TryGetComponent<NetworkTransform>(out var victimNt)) {
          victimNt.DisableSharedModeInterpolation = true;
        }

        var brain = mob.GetComponent<NetworkMobBrain>();
        var victimHealth = victim.GetComponent<Health>();

        yield return WaitFrames(6);
        AuthorityTeleportTo(mob, spawnMob);
        AuthorityTeleportTo(victim, victimNear);
        brain.RefreshWanderOriginAuthority();

        yield return WaitFrames(5);
        victimHealth.AuthorityApplyStartingHealthIfUnset();

        yield return WaitUntil(
          () => mob.IsValid && mob.transform.position.x > spawnMob.x + 1.1f,
          maxFrames: 900,
          failureMessage: () => "Mob did not approach victim before leash test.");

        yield return WaitFrames(4);

        AuthorityTeleportTo(victim, victimKited);
        yield return WaitFrames(6);

        float distMobToSpawn0 = HorizDist(mob.transform.position, spawnMob);
        yield return WaitUntil(
          () => mob.IsValid && HorizDist(mob.transform.position, spawnMob) < distMobToSpawn0 - 0.06f,
          maxFrames: 1200,
          failureMessage: () => "Mob should move back toward spawn after target exceeds leash.");

        float hpAfterKite = victimHealth.NetworkedHealth;
        const int settleFrames = 120;
        for (var i = 0; i < settleFrames; i++) {
          yield return new WaitForFixedUpdate();
          yield return null;
        }

        Assert.LessOrEqual(
          victimHealth.StartingHealth - victimHealth.NetworkedHealth,
          (victimHealth.StartingHealth - hpAfterKite) + 2.5f,
          "Victim should not keep taking substantial melee damage while far outside leash (smoke tolerance).");
      }
    }

    static void ConfigureMobForChaseScenario(NetworkMobBrain brain) {
      if (brain == null) {
        return;
      }

      brain.WanderRadius = 0f;
      brain.MoveSpeed = 0f;
      brain.MinLegDistance = 0f;
      brain.IdleTicksMin = 1;
      brain.IdleTicksMax = 1;
      brain.ChaseSpeed = 14f;
      brain.LeashRadius = 50f;
      brain.AggroRadius = 14f;
      brain.AttackRange = 1.4f;
      brain.AttackDamage = 4f;
      brain.AttackIntervalSeconds = 0.08f;
    }

    static float HorizDist(Vector3 a, Vector3 b) {
      return Mathf.Sqrt(NetworkMobBrainLogic.HorizontalSqrDistance(a, b));
    }

    static void AuthorityTeleportTo(NetworkObject obj, Vector3 worldPos) {
      Quaternion rot = Quaternion.identity;
      if (obj.TryGetComponent<CharacterController>(out var cc)) {
        cc.enabled = false;
      }

      if (obj.TryGetComponent<NetworkTransform>(out var nt) && nt.HasStateAuthority) {
        nt.Teleport(worldPos, rot);
      } else {
        obj.transform.SetPositionAndRotation(worldPos, rot);
      }

      if (obj.TryGetComponent<CharacterController>(out var cc2)) {
        cc2.enabled = true;
      }
    }

    static IEnumerator WaitUntil(Func<bool> predicate, int maxFrames, Func<string> failureMessage = null) {
      int i = 0;
      while (i < maxFrames && !predicate()) {
        i++;
        yield return new WaitForFixedUpdate();
        yield return null;
      }

      if (predicate()) {
        yield break;
      }

      string extra = failureMessage != null ? " " + failureMessage() : "";
      Assert.Fail($"Predicate not satisfied within {maxFrames} frames.{extra}");
    }

    static IEnumerator WaitFrames(int count) {
      for (var i = 0; i < count; i++) {
        yield return new WaitForFixedUpdate();
        yield return null;
      }
    }
  }
}
