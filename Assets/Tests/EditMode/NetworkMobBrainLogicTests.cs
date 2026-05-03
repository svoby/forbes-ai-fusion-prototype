using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  [TestFixture]
  public class NetworkMobBrainLogicTests {
    [Test]
    public void PickDestinationXZ_NegativeRadius_ReturnsOrigin() {
      var origin = new Vector3(1f, 2f, 3f);
      var got = NetworkMobBrainLogic.PickDestinationXZ(origin, -3f, 0.25f, 0.75f);
      Assert.AreEqual(origin, got);
    }

    [Test]
    public void PickDestinationXZ_UnitRadius_OnBoundary() {
      var origin = new Vector3(5f, 1f, -2f);
      var got = NetworkMobBrainLogic.PickDestinationXZ(origin, 4f, 1f, 0f);
      Assert.AreEqual(9f, got.x, 1e-5f);
      Assert.AreEqual(origin.y, got.y, 1e-5f);
      Assert.AreEqual(-2f, got.z, 1e-5f);
    }

    [Test]
    public void HorizontalDistance_FromPickedPoint_IsWithinRadius() {
      var origin = Vector3.zero;
      float radius = 7.5f;
      var dest = NetworkMobBrainLogic.PickDestinationXZ(origin, radius, 0.37f, 0.91f);
      float horiz = Mathf.Sqrt(NetworkMobBrainLogic.HorizontalSqrDistance(origin, dest));
      Assert.LessOrEqual(horiz, radius + 1e-4f);
    }

    [Test]
    public void ShouldLeaveIdle_WhenTickReached_ReturnsTrue() {
      Assert.IsTrue(NetworkMobBrainLogic.ShouldLeaveIdle(NetworkMobBrainState.Idle, 10, 10));
      Assert.IsFalse(NetworkMobBrainLogic.ShouldLeaveIdle(NetworkMobBrainState.Wander, 10, 10));
    }

    [Test]
    public void HasArrivedHorizontally_InsideThreshold_ReturnsTrue() {
      var pos = new Vector3(1f, 0f, 1f);
      var dest = new Vector3(1.05f, 5f, 1.02f);
      Assert.IsTrue(NetworkMobBrainLogic.HasArrivedHorizontally(pos, dest, 0.1f));
    }

    [Test]
    public void TryGetHorizontalDirection_IgnoresY_UsesXZOnly() {
      var from = new Vector3(0f, 100f, 0f);
      var to = new Vector3(3f, -50f, 4f);
      Assert.IsTrue(NetworkMobBrainLogic.TryGetHorizontalDirection(from, to, out var dir));
      var expected = new Vector3(3f, 0f, 4f).normalized;
      Assert.AreEqual(expected.x, dir.x, 1e-5f);
      Assert.AreEqual(expected.y, dir.y, 1e-5f);
      Assert.AreEqual(expected.z, dir.z, 1e-5f);
    }

    [Test]
    public void TryGetHorizontalDirection_WhenValid_IsUnitLength() {
      var from = Vector3.zero;
      var to = new Vector3(2f, 0f, 2f);
      Assert.IsTrue(NetworkMobBrainLogic.TryGetHorizontalDirection(from, to, out var dir));
      Assert.AreEqual(1f, dir.magnitude, 1e-5f);
      Assert.AreEqual(0f, dir.y, 1e-5f);
    }

    [Test]
    public void TryGetHorizontalDirection_SameXZ_ReturnsFalseAndZero() {
      var from = new Vector3(1f, 0f, 2f);
      var to = new Vector3(1f, 9f, 2f);
      Assert.IsFalse(NetworkMobBrainLogic.TryGetHorizontalDirection(from, to, out var dir));
      Assert.AreEqual(0f, dir.x, 1e-5f);
      Assert.AreEqual(0f, dir.y, 1e-5f);
      Assert.AreEqual(0f, dir.z, 1e-5f);
    }

    [Test]
    public void RotationFacingHorizontal_RightForward_PointsPositiveX() {
      var q = NetworkMobBrainLogic.RotationFacingHorizontal(Vector3.right, Quaternion.identity);
      Vector3 worldForward = q * Vector3.forward;
      Assert.AreEqual(1f, worldForward.x, 1e-4f);
      Assert.AreEqual(0f, worldForward.y, 1e-4f);
      Assert.AreEqual(0f, worldForward.z, 1e-4f);
    }

    [Test]
    public void RotationFacingHorizontal_ZeroOrTiny_PreservesFallback() {
      var fallback = Quaternion.Euler(0f, 47f, 0f);
      var qZero = NetworkMobBrainLogic.RotationFacingHorizontal(Vector3.zero, fallback);
      Assert.Less(Quaternion.Angle(qZero, fallback), 1e-3f);

      var qTiny = NetworkMobBrainLogic.RotationFacingHorizontal(new Vector3(1e-5f, 0f, 1e-5f), fallback);
      Assert.Less(Quaternion.Angle(qTiny, fallback), 1e-3f);
    }
  }
}
