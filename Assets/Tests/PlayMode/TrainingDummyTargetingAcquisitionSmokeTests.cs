using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Validates the player-facing targeting boundary: enumerate eligible training mobs and resolve them via layered physics rays,
  /// matching what <see cref="TargetingController"/> does at runtime without relying on brittle hardware mouse injection.
  /// </summary>
  [TestFixture]
  public class TrainingDummyTargetingAcquisitionSmokeTests {
    FusionSinglePlayerTestSession _session;
    GameObject                    _testCamera;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session      = new FusionSinglePlayerTestSession();
      _testCamera   = null;
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
    public IEnumerator SinglePlayer_TabCycle_And_MouseRay_FindTrainingDummyNetworkTarget() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkObject player = null;
        NetworkObject dummy  = null;

        var runner = _session.Runner;
        Assert.IsNotNull(runner);
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, -4f);
        var spawnFlags  = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => player = o);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => dummy = o);

        if (dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        Assert.IsTrue(dummy.TryGetComponent(out Health dummyHealth));
        dummyHealth.AuthorityApplyStartingHealthIfUnset();

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => dummy.IsValid &&
                Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth) &&
                !dummyHealth.IsDead,
          480,
          messageOnFail: $"Dummy stabilize: hp={dummyHealth.NetworkedHealth} dead={dummyHealth.IsDead}");

        _testCamera = new GameObject("Main Camera");
        _testCamera.tag = "MainCamera";
        Camera mainCam  = _testCamera.AddComponent<Camera>();
        _testCamera.transform.position = spawnPlayer + new Vector3(0f, 2.75f, -5f);
        _testCamera.transform.LookAt(spawnDummy + Vector3.up * 1f);

        PlayModeTargetingCleanup.DestroyTargetingSystemsDuringFusionSession();
        yield return null;
        yield return null;

        var sysGo = new GameObject("[TargetingSystem]");
        sysGo.AddComponent<LineRenderer>();
        sysGo.AddComponent<TargetHighlight>();
        sysGo.AddComponent<SelectedTargetHealthBar>();
        var targeting = sysGo.AddComponent<TargetingController>();

        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        Assert.IsTrue(dummy.TryGetComponent(out Targetable dummyTarget));

        InvokeCycleTarget(targeting);

        yield return FusionPlayModeTestHelpers.WaitFrames(3);

        Assert.AreSame(dummyTarget, targeting.CurrentTarget, "CycleTarget must land on training dummy candidate.");
        Assert.AreEqual(dummy.Id.Raw, targeting.CurrentTargetId.Raw, "GameplayInput consumes NetworkId parity.");

        Vector3 screenPt = mainCam.WorldToScreenPoint(dummy.transform.position + Vector3.up);
        Assert.Greater(screenPt.z,
          0.5f,
          "Dummy should remain in front of the camera frustrum so Editor PlayMode rays stay deterministic.");
        var ray = mainCam.ScreenPointToRay(screenPt);

        Targetable resolved = TargetingAcquisitionLogic.TryPickSelectableAlongRay(
          in ray,
          200f,
          runner,
          out bool physicsHit,
          out RaycastHit _);

        Assert.IsTrue(physicsHit, "Layered Fusion/default physics rays should strike the prefab-backed collider.");
        Assert.AreSame(dummyTarget,
          resolved,
          "Collider hit should resolve Parent Targetable identical to networked dummy.");
        Assert.IsNotNull(player);
      }
    }

    /// <summary>
    /// PR #20 review regression: <see cref="TargetingController"/> LMB path must call <c>EnsureCamera()</c> before
    /// evaluating <see cref="ThirdPersonOrbitCamera.IsLmbDragging"/> (same helper as runtime mouse ray, minus hardware).
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SinglePlayer_LmbController_OrbitGate_BeforeRaySelect() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkObject dummy = null;

        var runner = _session.Runner;
        Assert.IsNotNull(runner);
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, -4f);
        var spawnFlags  = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner,
          playerPrefab,
          spawnPlayer,
          Quaternion.identity,
          spawnFlags,
          _ => { });

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => dummy = o);

        if (dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        Assert.IsTrue(dummy.TryGetComponent(out Health dummyHealth));
        dummyHealth.AuthorityApplyStartingHealthIfUnset();

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => dummy.IsValid &&
                Mathf.Approximately(dummyHealth.NetworkedHealth, dummyHealth.StartingHealth) &&
                !dummyHealth.IsDead,
          480,
          messageOnFail: $"Dummy stabilize: hp={dummyHealth.NetworkedHealth} dead={dummyHealth.IsDead}");

        _testCamera = new GameObject("Main Camera");
        _testCamera.tag           = "MainCamera";
        Camera mainCam           = _testCamera.AddComponent<Camera>();
        var orbitCam             = _testCamera.AddComponent<ThirdPersonOrbitCamera>();
        orbitCam.enabled         = false; // keep IsLmbDragging under test-only control (LateUpdate resets with real mice)

        _testCamera.transform.position = spawnPlayer + new Vector3(0f, 2.75f, -5f);
        _testCamera.transform.LookAt(spawnDummy + Vector3.up * 1f);

        PlayModeTargetingCleanup.DestroyTargetingSystemsDuringFusionSession();
        yield return null;
        yield return null;

        var sysGo = new GameObject("[TargetingSystem]");
        sysGo.AddComponent<LineRenderer>();
        sysGo.AddComponent<TargetHighlight>();
        sysGo.AddComponent<SelectedTargetHealthBar>();
        var targeting = sysGo.AddComponent<TargetingController>();

        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        Assert.IsTrue(dummy.TryGetComponent(out Targetable dummyTarget));

        Vector3 screenPt = mainCam.WorldToScreenPoint(dummy.transform.position + Vector3.up);
        Assert.Greater(
          screenPt.z,
          0.5f,
          "Dummy should remain in front of the camera frustum so LMB rays stay deterministic.");
        Ray ray = mainCam.ScreenPointToRay(screenPt);

        orbitCam.SetIsLmbDraggingForTests(true);
        targeting.ProcessLmbReleasedSelectionForTests(in ray);
        Assert.IsNull(targeting.CurrentTarget,
          "Dragging orbit LMB must suppress world ray selection only when ThirdPersonOrbitCamera is bound via EnsureCamera.");

        orbitCam.SetIsLmbDraggingForTests(false);
        targeting.ProcessLmbReleasedSelectionForTests(in ray);
        Assert.AreSame(dummyTarget,
          targeting.CurrentTarget,
          "Explicit-ray path must match fused physics resolution used by LMB release after drag gate clears.");
      }
    }

    static void InvokeCycleTarget(TargetingController targeting) {
      MethodInfo mi = typeof(TargetingController).GetMethod(
        "CycleTarget",
        BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.IsNotNull(mi);
      mi.Invoke(targeting, null);
    }
  }
}
