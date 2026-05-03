using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Verifies authority wander ticks produce non-zero displacement (via replicated transform).
  /// Uses <see cref="FusionPlayModeTestHelpers.WaitUntil"/> with <c>physicsThenRenderEachStep: false</c> to match prior timing.
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
        brain.WanderRadius     = 26f;
        brain.WalkSpeed        = 14f;
        brain.MinLegDistance   = 12f;
        brain.ArrivalThreshold = 0.18f;
        brain.IdleTicksMin     = 1;
        brain.IdleTicksMax     = 3;

        if (dummy.TryGetComponent<NetworkTransform>(out var mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(16, physicsThenRenderEachStep: false);

        Vector3 p0 = dummy.transform.position;

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => NetworkMobBrainLogic.HorizontalSqrDistance(p0, dummy.transform.position) > 0.12f * 0.12f,
          900,
          physicsThenRenderEachStep: false);

        Vector3 p1 = dummy.transform.position;
        Assert.IsTrue(
          NetworkMobBrainLogic.TryGetHorizontalDirection(p0, p1, out _),
          "expected non-zero horizontal travel for movement smoke test.");
      }
    }
  }
}
