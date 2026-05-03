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
  }
}
