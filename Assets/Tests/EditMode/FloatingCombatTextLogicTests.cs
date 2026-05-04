using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// EditMode unit tests for <see cref="FloatingCombatTextLogic"/>.
  /// All methods under test are pure static math — no scene, no runner, no camera.
  /// </summary>
  [TestFixture]
  public class FloatingCombatTextLogicTests {
    const float Eps = 1e-4f;

    // ── ComputeAlpha ──────────────────────────────────────────────────────────

    [Test]
    public void ComputeAlpha_AtElapsedZero_IsOne() {
      Assert.AreEqual(1f, FloatingCombatTextLogic.ComputeAlpha(0f, 1f), Eps,
        "Alpha at elapsed=0 must be fully opaque.");
    }

    [Test]
    public void ComputeAlpha_AtElapsedEqualToLifetime_IsZero() {
      Assert.AreEqual(0f, FloatingCombatTextLogic.ComputeAlpha(1f, 1f), Eps,
        "Alpha at elapsed==lifetime must be fully transparent.");
    }

    [Test]
    public void ComputeAlpha_AtHalfLifetime_IsOneInsideFadeThreshold() {
      // Half-lifetime is 0.5 which is below the 0.6 fade-start threshold.
      Assert.AreEqual(1f, FloatingCombatTextLogic.ComputeAlpha(0.5f, 1f), Eps,
        "Alpha at t=0.5 (before fade threshold 0.6) must still be 1.");
    }

    [Test]
    public void ComputeAlpha_AtEightyPercentLifetime_IsBetweenZeroAndOne() {
      float alpha = FloatingCombatTextLogic.ComputeAlpha(0.8f, 1f);
      Assert.Greater(alpha, 0f,  "Alpha at t=0.8 should not yet be zero.");
      Assert.Less   (alpha, 1f,  "Alpha at t=0.8 should have started fading.");
    }

    [Test]
    public void ComputeAlpha_WithZeroLifetime_IsZero() {
      Assert.AreEqual(0f, FloatingCombatTextLogic.ComputeAlpha(0f, 0f), Eps,
        "Zero-lifetime edge case: alpha must be 0 to avoid divide-by-zero artifacts.");
    }

    [Test]
    public void ComputeAlpha_WithElapsedBeyondLifetime_Clamps() {
      float alpha = FloatingCombatTextLogic.ComputeAlpha(5f, 1f);
      Assert.AreEqual(0f, alpha, Eps, "Elapsed beyond lifetime must clamp to 0.");
    }

    // ── ComputePixelOffset ────────────────────────────────────────────────────

    [Test]
    public void ComputePixelOffset_AtElapsedZero_IsZero() {
      Assert.AreEqual(0f, FloatingCombatTextLogic.ComputePixelOffset(0f, 1f, 80f), Eps,
        "Pixel offset at start must be 0.");
    }

    [Test]
    public void ComputePixelOffset_AtElapsedEqualToLifetime_IsMaxPixels() {
      Assert.AreEqual(80f, FloatingCombatTextLogic.ComputePixelOffset(1f, 1f, 80f), Eps,
        "Pixel offset at end of lifetime must equal maxPixels.");
    }

    [Test]
    public void ComputePixelOffset_AtMidLifetime_IsBetweenZeroAndMax() {
      float offset = FloatingCombatTextLogic.ComputePixelOffset(0.5f, 1f, 80f);
      Assert.Greater(offset, 0f,  "Mid-lifetime offset should be above zero.");
      Assert.Less   (offset, 80f, "Mid-lifetime offset should be below max.");
    }

    [Test]
    public void ComputePixelOffset_IsMonotonicallyIncreasing() {
      float o1 = FloatingCombatTextLogic.ComputePixelOffset(0.2f, 1f, 100f);
      float o2 = FloatingCombatTextLogic.ComputePixelOffset(0.5f, 1f, 100f);
      float o3 = FloatingCombatTextLogic.ComputePixelOffset(0.8f, 1f, 100f);
      Assert.Less(o1, o2, "Offset at t=0.2 should be less than at t=0.5.");
      Assert.Less(o2, o3, "Offset at t=0.5 should be less than at t=0.8.");
    }

    [Test]
    public void ComputePixelOffset_WithZeroLifetime_ReturnsMaxPixels() {
      Assert.AreEqual(100f, FloatingCombatTextLogic.ComputePixelOffset(0f, 0f, 100f), Eps,
        "Zero-lifetime edge case must return maxPixels immediately.");
    }

    // ── IsBehindCamera ────────────────────────────────────────────────────────

    [Test]
    public void IsBehindCamera_PositiveZ_ReturnsFalse() {
      Assert.IsFalse(FloatingCombatTextLogic.IsBehindCamera(new Vector3(100f, 200f, 5f)),
        "Positive z means the point is in front of the camera.");
    }

    [Test]
    public void IsBehindCamera_ZeroZ_ReturnsTrue() {
      Assert.IsTrue(FloatingCombatTextLogic.IsBehindCamera(new Vector3(100f, 200f, 0f)),
        "z=0 is on the camera plane and should be treated as hidden.");
    }

    [Test]
    public void IsBehindCamera_NegativeZ_ReturnsTrue() {
      Assert.IsTrue(FloatingCombatTextLogic.IsBehindCamera(new Vector3(100f, 200f, -1f)),
        "Negative z means the point is behind the camera.");
    }
  }
}
