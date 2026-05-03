using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  [TestFixture]
  public class SelectedTargetHealthBarSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject                 _dummy;
    GameObject                    _testCamera;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _dummy   = null;
      _testCamera = null;
    }

    [TearDown]
    public void TearDown() {
      if (_testCamera != null) {
        Object.DestroyImmediate(_testCamera);
        _testCamera = null;
      }
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SelectedTarget_HealthBar_VisibleFill_HidesWhenClearedOrDead() {
      var dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        _testCamera = new GameObject("Main Camera");
        _testCamera.tag = "MainCamera";
        var cam = _testCamera.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 2.5f, -9f);
        cam.transform.LookAt(new Vector3(2f, 0.5f, 0f));

        var runner = _session.Runner;
        Assert.IsNotNull(runner);

        // StartGame can bootstrap [TargetingSystem] again. Do NOT call DestroyAutoCreatedTargetingSystem() here:
        // it also destroys orphaned FusionSinglePlayerTestSession_* hosts — including the active runner root.
        PlayModeTargetingCleanup.DestroyTargetingSystemsDuringFusionSession();
        yield return null;
        yield return null;

        var sysGo = new GameObject("[TargetingSystem]");
        sysGo.AddComponent<LineRenderer>();
        sysGo.AddComponent<TargetHighlight>();
        var targeting = sysGo.AddComponent<TargetingController>();
        var healthBar = sysGo.AddComponent<SelectedTargetHealthBar>();

        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnDummy = new Vector3(2f, 0f, 0f);
        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return null;

        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));
        Assert.IsTrue(_dummy.TryGetComponent(out Targetable targetable));
        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => _dummy.IsValid && Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth),
          480,
          messageOnFail: "Dummy HP settle.");

        InvokeSetTarget(targeting, targetable);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => healthBar.IsBarVisible, 60, messageOnFail: "Health bar should appear.");

        Assert.AreEqual(1f, healthBar.CurrentFill01, 0.04f);

        dummyHealth.DealDamageRpc(dummyHealth.StartingHealth * 0.5f);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Abs(healthBar.CurrentFill01 - 0.5f) < 0.1f,
          180,
          messageOnFail:
          $"Fill ~0.5 (fill={healthBar.CurrentFill01} hp={dummyHealth.NetworkedHealth}).");

        Assert.IsTrue(healthBar.IsBarVisible);

        InvokeSetTarget(targeting, null);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => !healthBar.IsBarVisible, 60, messageOnFail: "Bar should hide when cleared.");

        InvokeSetTarget(targeting, targetable);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => healthBar.IsBarVisible, 60);

        dummyHealth.DealDamageRpc(dummyHealth.StartingHealth * 2f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => dummyHealth.IsDead, 480);
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => !healthBar.IsBarVisible, 90, messageOnFail: "Bar should hide for dead target.");
      }
    }

    static void InvokeSetTarget(TargetingController targeting, Targetable target) {
      var mi = typeof(TargetingController).GetMethod(
        "SetTarget",
        BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.IsNotNull(mi);
      mi.Invoke(targeting, new object[] { target });
    }
  }
}
