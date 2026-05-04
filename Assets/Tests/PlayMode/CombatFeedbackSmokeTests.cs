using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion Single-player smoke: verifies that <see cref="Health.DealDamageRpc"/> on State
  /// Authority increments <see cref="Health.LastHitEventSeq"/> and records
  /// <see cref="Health.LastHitDamage"/> and <see cref="Health.LastHitTick"/> as expected.
  /// Cosmetic effect (<see cref="HitImpactView"/>) is not tested here; it requires the
  /// component to be added to a prefab and is covered by manual verification.
  /// </summary>
  [TestFixture]
  public class CombatFeedbackSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject                 _dummy;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _dummy   = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator DealDamageRpc_IncreasesLastHitEventSeq_AndRecordsDamageAndTick() {
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab, "TrainingDummy prefab missing.");

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner     = _session.Runner;
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        var spawnPos   = new Vector3(0f, 0f, 8f);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnPos, Quaternion.identity,
          PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent<NetworkMobBrain>(out var brain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(brain);
        }

        if (_dummy.TryGetComponent<NetworkTransform>(out var nt)) {
          nt.DisableSharedModeInterpolation = true;
        }

        Assert.IsTrue(_dummy.TryGetComponent(out Health health),
          "TrainingDummy must have a Health component.");
        Assert.IsTrue(health.HasStateAuthority,
          "DealDamageRpc requires state authority; test must run as authority.");

        health.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Approximately(health.NetworkedHealth, health.StartingHealth),
          480,
          messageOnFail: $"Dummy HP never reached StartingHealth. hp={health.NetworkedHealth}");

        byte  seqBefore = health.LastHitEventSeq;
        float hitDamage = 15f;

        health.DealDamageRpc(hitDamage);

        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => health.LastHitEventSeq != seqBefore,
          480,
          buildFailureDetails: () =>
            $"LastHitEventSeq did not change after DealDamageRpc. " +
            $"before={seqBefore} current={health.LastHitEventSeq} " +
            $"hp={health.NetworkedHealth} sa={health.HasStateAuthority}");

        byte expectedSeq = unchecked((byte)(seqBefore + 1));
        Assert.AreEqual(expectedSeq, health.LastHitEventSeq,
          "LastHitEventSeq should increment by 1 after a single hit.");
        Assert.AreEqual(hitDamage, health.LastHitDamage, 1e-3f,
          "LastHitDamage should record the requested damage amount.");
        Assert.Greater(health.LastHitTick, 0,
          "LastHitTick should be a positive simulation tick.");

        // HP must still reflect the damage (event does not bypass HP deduction).
        float expectedHp = health.StartingHealth - hitDamage;
        Assert.AreEqual(expectedHp, health.NetworkedHealth, 1e-3f,
          "DealDamageRpc must reduce HP even when recording the hit event.");
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator DealDamageRpc_MultipleHits_SeqIncrementsEachTime() {
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab, "TrainingDummy prefab missing.");

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner     = _session.Runner;
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, new Vector3(0f, 0f, 10f), Quaternion.identity,
          PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent<NetworkMobBrain>(out var brain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(brain);
        }

        if (_dummy.TryGetComponent<NetworkTransform>(out var nt)) {
          nt.DisableSharedModeInterpolation = true;
        }

        Assert.IsTrue(_dummy.TryGetComponent(out Health health));

        health.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Approximately(health.NetworkedHealth, health.StartingHealth),
          480);

        // First hit.
        byte seq0 = health.LastHitEventSeq;
        health.DealDamageRpc(5f);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => health.LastHitEventSeq != seq0, 480,
          messageOnFail: "Seq did not change after first hit.");
        byte seq1 = health.LastHitEventSeq;

        // Second hit.
        health.DealDamageRpc(5f);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => health.LastHitEventSeq != seq1, 480,
          messageOnFail: "Seq did not change after second hit.");
        byte seq2 = health.LastHitEventSeq;

        Assert.AreEqual(unchecked((byte)(seq0 + 1)), seq1, "Seq after hit 1.");
        Assert.AreEqual(unchecked((byte)(seq0 + 2)), seq2, "Seq after hit 2.");
      }
    }
  }
}
