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
      Assert.IsFalse(NetworkMobBrainLogic.ShouldLeaveIdle(NetworkMobBrainState.Chase, 10, 10));
      Assert.IsFalse(NetworkMobBrainLogic.ShouldLeaveIdle(NetworkMobBrainState.Return, 10, 10));
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

    [Test]
    public void IsWithinHorizontalRange_SameXZ_DifferentY_IsInside() {
      var a = new Vector3(1f, 0f, 2f);
      var b = new Vector3(1f, 999f, 2f);
      Assert.IsTrue(NetworkMobBrainLogic.IsWithinHorizontalRange(a, b, 0.05f));
    }

    [Test]
    public void IsWithinHorizontalRange_OutsideHorizontalRange_ReturnsFalse() {
      var a = Vector3.zero;
      var b = new Vector3(3f, 0f, 4f);
      Assert.IsFalse(NetworkMobBrainLogic.IsWithinHorizontalRange(a, b, 4.99f));
      Assert.IsTrue(NetworkMobBrainLogic.IsWithinHorizontalRange(a, b, 5.01f));
    }

    [Test]
    public void IsWithinHorizontalRange_NegativeRange_ClampedToZero() {
      var a = Vector3.zero;
      var b = new Vector3(1f, 0f, 0f);
      Assert.IsFalse(NetworkMobBrainLogic.IsWithinHorizontalRange(a, b, -2f));
    }

    [Test]
    public void CanAttackAtTick_WhenBeforeCooldown_False() {
      Assert.IsFalse(NetworkMobBrainLogic.CanAttackAtTick(4, 5));
      Assert.IsTrue(NetworkMobBrainLogic.CanAttackAtTick(5, 5));
      Assert.IsTrue(NetworkMobBrainLogic.CanAttackAtTick(6, 5));
    }

    [Test]
    public void UsesCasterCombat_OnlyCasterMode_ReturnsTrue() {
      Assert.IsFalse(NetworkMobBrainLogic.UsesCasterCombat(NetworkMobBrainCombatMode.Melee));
      Assert.IsTrue(NetworkMobBrainLogic.UsesCasterCombat(NetworkMobBrainCombatMode.Caster));
    }

    [Test]
    public void ShouldHoldForCasterCast_UsesHorizontalSpellRange() {
      var caster = new Vector3(0f, 0f, 0f);
      var inside = new Vector3(3f, 20f, 4f);
      var outside = new Vector3(6f, 0f, 8f);

      Assert.IsTrue(NetworkMobBrainLogic.ShouldHoldForCasterCast(caster, inside, 5f));
      Assert.IsFalse(NetworkMobBrainLogic.ShouldHoldForCasterCast(caster, outside, 9.99f));
    }

    [Test]
    public void SecondsToTicks_AlwaysAtLeastOne() {
      Assert.AreEqual(1, NetworkMobBrainLogic.SecondsToTicks(0f, 60));
      Assert.AreEqual(1, NetworkMobBrainLogic.SecondsToTicks(0.001f, 60));
      Assert.AreEqual(1, NetworkMobBrainLogic.SecondsToTicks(1f, 1));
      Assert.AreEqual(60, NetworkMobBrainLogic.SecondsToTicks(1f, 60));
    }

    [Test]
    public void HasArrivedHorizontally_ReturnIgnoresY() {
      var spawn = new Vector3(10f, 0f, -5f);
      var mob = new Vector3(10.02f, 99f, -4.98f);
      Assert.IsTrue(NetworkMobBrainLogic.HasArrivedHorizontally(mob, spawn, 0.15f));
    }

    [Test]
    public void IsBeyondLeash_IgnoresY() {
      var spawn = new Vector3(0f, 0f, 0f);
      var p = new Vector3(3f, 100f, 4f);
      Assert.IsFalse(NetworkMobBrainLogic.IsBeyondLeash(spawn, p, 5f));
      Assert.IsTrue(NetworkMobBrainLogic.IsBeyondLeash(spawn, p, 4.9f));
    }

    [Test]
    public void IsBeyondLeash_NegativeRadius_ClampedToZero() {
      var spawn = Vector3.zero;
      var p = new Vector3(0.1f, 0f, 0f);
      Assert.IsTrue(NetworkMobBrainLogic.IsBeyondLeash(spawn, p, -3f));
    }

    [Test]
    public void IsBeyondLeash_AtBoundary_NotBeyond() {
      var spawn = Vector3.zero;
      var p = new Vector3(3f, 0f, 4f);
      Assert.IsFalse(NetworkMobBrainLogic.IsBeyondLeash(spawn, p, 5f));
    }

    [Test]
    public void ShouldAbortChaseAndReturn_InvalidTarget_ReturnsTrue() {
      Assert.IsTrue(NetworkMobBrainLogic.ShouldAbortChaseAndReturn(
        Vector3.zero, Vector3.zero, new Vector3(1f, 0f, 0f), 10f, false));
    }

    [Test]
    public void ShouldAbortChaseAndReturn_MobPastLeash_ReturnsTrue() {
      Assert.IsTrue(NetworkMobBrainLogic.ShouldAbortChaseAndReturn(
        Vector3.zero, new Vector3(11f, 0f, 0f), new Vector3(1f, 0f, 0f), 10f, true));
    }

    [Test]
    public void ShouldAbortChaseAndReturn_TargetPastLeash_ReturnsTrue() {
      Assert.IsTrue(NetworkMobBrainLogic.ShouldAbortChaseAndReturn(
        Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(11f, 0f, 0f), 10f, true));
    }

    [Test]
    public void ShouldAbortChaseAndReturn_ValidInsideLeash_ReturnsFalse() {
      Assert.IsFalse(NetworkMobBrainLogic.ShouldAbortChaseAndReturn(
        Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(-2f, 0f, 2f), 10f, true));
    }

    [Test]
    public void SecondsToTicks_NonPositiveTickRate_UsesOne() {
      Assert.AreEqual(3, NetworkMobBrainLogic.SecondsToTicks(3f, 0));
      Assert.AreEqual(3, NetworkMobBrainLogic.SecondsToTicks(3f, -5));
    }

    // --- SelectMobSpeed ---

    [Test]
    public void SelectMobSpeed_IdleAndWander_ReturnsWalkSpeed() {
      Assert.AreEqual(3f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Idle,   3f, 6f), 1e-5f);
      Assert.AreEqual(3f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Wander, 3f, 6f), 1e-5f);
    }

    [Test]
    public void SelectMobSpeed_ChaseAndReturn_ReturnsRunSpeed() {
      Assert.AreEqual(6f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Chase,  3f, 6f), 1e-5f);
      Assert.AreEqual(6f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Return, 3f, 6f), 1e-5f);
    }

    [Test]
    public void SelectMobSpeed_NegativeWalkSpeed_ClampedToZero() {
      Assert.AreEqual(0f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Wander, -5f, 6f), 1e-5f);
    }

    [Test]
    public void SelectMobSpeed_NegativeRunSpeed_ClampedToZero() {
      Assert.AreEqual(0f, NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Chase, 3f, -6f), 1e-5f);
    }

    [Test]
    public void SelectMobSpeed_RunSpeedDoubleWalk_MatchesExpected() {
      float walk = 3f;
      float run  = 6f;
      Assert.AreEqual(run, walk * 2f, 1e-5f,
        "Default run speed should be twice the walk speed.");
      Assert.AreEqual(walk,
        NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Wander, walk, run), 1e-5f);
      Assert.AreEqual(run,
        NetworkMobBrainLogic.SelectMobSpeed(NetworkMobBrainState.Chase,  walk, run), 1e-5f);
    }
  }
}
