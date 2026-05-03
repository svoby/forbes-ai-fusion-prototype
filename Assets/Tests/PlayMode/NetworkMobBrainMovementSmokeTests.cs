using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Verifies authority wander ticks produce non-zero displacement (via replicated transform).
  /// </summary>
  [TestFixture]
  public class NetworkMobBrainMovementSmokeTests {
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
    public IEnumerator FusionSingle_TrainingDummy_WithMobBrain_PositionChangesOverTicks() {
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab);
      Assert.IsNotNull(dummyPrefab.GetComponent<NetworkMobBrain>());

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        NetworkObject dummy = null;
        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner,
          dummyPrefab,
          new Vector3(30f, 0f, -12f),
          Quaternion.identity,
          PlayerRef.None,
          NetworkSpawnFlags.SharedModeStateAuthLocalPlayer,
          o => dummy = o);

        var brain = dummy.GetComponent<NetworkMobBrain>();
        Assert.IsNotNull(brain);
        brain.WanderRadius = 26f;
        brain.MoveSpeed = 14f;
        brain.MinLegDistance = 12f;
        brain.ArrivalThreshold = 0.18f;
        brain.IdleTicksMin = 1;
        brain.IdleTicksMax = 3;

        yield return WaitFrames(16);

        Vector3 p0 = dummy.transform.position;

        yield return WaitUntil(
          () => NetworkMobBrainLogic.HorizontalSqrDistance(p0, dummy.transform.position) > 0.12f * 0.12f,
          maxFrames: 900);

        Vector3 p1 = dummy.transform.position;
        Assert.IsTrue(
          NetworkMobBrainLogic.TryGetHorizontalDirection(p0, p1, out var travelDir),
          "expected non-zero horizontal travel for facing smoke test.");

        Vector3 f = dummy.transform.forward;
        var forwardHz = new Vector3(f.x, 0f, f.z);
        Assert.Greater(forwardHz.sqrMagnitude, 1e-6f, "horizontal forward should be non-degenerate.");
        forwardHz.Normalize();

        float dot = Vector3.Dot(travelDir, forwardHz);
        Assert.Greater(dot, 0.7f, $"mob should generally face travel direction (dot={dot}).");
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
