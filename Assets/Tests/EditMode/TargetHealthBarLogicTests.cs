using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.UI;

namespace Forbes.Tests.EditMode {
  [TestFixture]
  public class TargetHealthBarLogicTests {
    [Test]
    public void ComputeHealthFill_Full_ReturnsOne() {
      Assert.AreEqual(1f, TargetHealthBarLogic.ComputeHealthFill(100f, 100f), 1e-6f);
    }

    [Test]
    public void ComputeHealthFill_Half_ReturnsHalf() {
      Assert.AreEqual(0.5f, TargetHealthBarLogic.ComputeHealthFill(50f, 100f), 1e-6f);
    }

    [Test]
    public void ComputeHealthFill_ZeroOrNegativeCurrent_ReturnsZero() {
      Assert.AreEqual(0f, TargetHealthBarLogic.ComputeHealthFill(0f, 100f), 1e-6f);
      Assert.AreEqual(0f, TargetHealthBarLogic.ComputeHealthFill(-10f, 100f), 1e-6f);
    }

    [Test]
    public void ComputeHealthFill_Overheal_ClampsToOne() {
      Assert.AreEqual(1f, TargetHealthBarLogic.ComputeHealthFill(150f, 100f), 1e-6f);
    }

    [Test]
    public void ComputeHealthFill_MaxZeroOrNegative_ReturnsZero() {
      Assert.AreEqual(0f, TargetHealthBarLogic.ComputeHealthFill(50f, 0f), 1e-6f);
      Assert.AreEqual(0f, TargetHealthBarLogic.ComputeHealthFill(50f, -1f), 1e-6f);
    }

    [Test]
    public void ComputeBillboardRotation_FacesCameraDirection() {
      var bar  = new Vector3(1f, 0f, 2f);
      var cam  = new Vector3(1f, 0f, -3f);
      var q    = TargetHealthBarLogic.ComputeBillboardRotation(bar, cam, Quaternion.identity, Vector3.up);
      var forward = q * Vector3.forward;
      var expected = (cam - bar).normalized;
      Assert.Less(Vector3.Angle(forward, expected), 0.05f);
    }

    [Test]
    public void ComputeBillboardRotation_ZeroDirection_PreservesFallback() {
      var p = new Vector3(3f, 2f, -1f);
      var fallback = Quaternion.Euler(12f, 55f, 7f);
      var r = TargetHealthBarLogic.ComputeBillboardRotation(p, p, fallback, Vector3.up);
      Assert.Less(Quaternion.Angle(fallback, r), 1e-3f);
    }

    [Test]
    public void ApplyHorizontalHpAnchors_Half_LeftAnchoredStripUsesAnchorMaxX() {
      GameObject canvasGo = null;
      try {
        canvasGo = new GameObject("Canvas_HPAnchors_Test");
        canvasGo.AddComponent<Canvas>();

        var fillGo = new GameObject("HpStrip");
        fillGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
        var strip = fillGo.AddComponent<RectTransform>();

        float t = TargetHealthBarLogic.ApplyHorizontalHpAnchors(strip, 50f, 100f);

        Assert.AreEqual(0.5f, t, 1e-6f);
        Assert.Less(Vector2.Distance(strip.anchorMin, Vector2.zero), 1e-5f);
        Assert.Less(Vector2.Distance(strip.anchorMax, new Vector2(0.5f, 1f)), 1e-5f);
      } finally {
        if (canvasGo != null) {
          Object.DestroyImmediate(canvasGo);
        }
      }
    }

    [Test]
    public void ApplyHorizontalHpAnchors_NullRect_ReturnsFillOnly_NoThrow() {
      float t = TargetHealthBarLogic.ApplyHorizontalHpAnchors(null, 25f, 100f);
      Assert.AreEqual(0.25f, t, 1e-6f);
    }
  }
}
