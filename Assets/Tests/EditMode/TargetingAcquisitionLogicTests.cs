using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  public class TargetingAcquisitionLogicTests {
    [Test]
    public void IsTabTargetingCandidate_Allows_VanillaMobWithNoMovementOrDeadFlag() {
      Assert.IsTrue(TargetingAcquisitionLogic.IsTabTargetingCandidate(
        movementClaimsLocalInputAuthority: false,
        targetHealthSaysDead: false));
    }

    [Test]
    public void IsTabTargetingCandidate_Rejects_PlayerMovement_WithLocalAuthority() {
      Assert.IsFalse(TargetingAcquisitionLogic.IsTabTargetingCandidate(
        movementClaimsLocalInputAuthority: true,
        targetHealthSaysDead: false));
    }

    [Test]
    public void IsTabTargetingCandidate_Rejects_DeadTargets() {
      Assert.IsFalse(TargetingAcquisitionLogic.IsTabTargetingCandidate(
        movementClaimsLocalInputAuthority: false,
        targetHealthSaysDead: true));
    }

    [Test]
    public void TryPickSelectableAlongRay_ResolvesTargetable_FromChildCollider_NoRunner() {
      var root       = new GameObject("tgt-root");
      var child      = new GameObject("collider-owner");
      child.transform.SetParent(root.transform);
      root.AddComponent<Targetable>();

      var col = child.AddComponent<BoxCollider>();
      col.center    = Vector3.zero;
      col.size      = Vector3.one;

      Ray ray       = new Ray(new Vector3(0f, 0.5f, -5f), Vector3.forward);
      float maxDist = 25f;

      var picked = TargetingAcquisitionLogic.TryPickSelectableAlongRay(
        in ray,
        maxDist,
        runner: null,
        out bool hitAnything,
        out RaycastHit _);

      Assert.IsTrue(hitAnything, "Default physics scene ray should strike the primitive collider.");
      Assert.IsNotNull(picked);
      Assert.AreSame(root.GetComponent<Targetable>(), picked);

      Object.DestroyImmediate(root);
    }
  }
}
