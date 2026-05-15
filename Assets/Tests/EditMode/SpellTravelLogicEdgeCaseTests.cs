using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Edge-case coverage for <see cref="SpellTravelLogic"/> pure math.
  /// Complements <see cref="SpellTravelLogicTests"/> with boundary and
  /// near-zero value scenarios.
  /// </summary>
  public class SpellTravelLogicEdgeCaseTests {

    // ── ComputeTravelTicks ───────────────────────────────────────────────

    [Test]
    public void ComputeTravelTicks_PositiveDistanceAndSpeed_ReturnsAtLeastOneTick() {
      // Any positive, non-trivial distance must yield ≥ 1 tick.
      int ticks = SpellTravelLogic.ComputeTravelTicks(1f, 20f, 60);
      Assert.GreaterOrEqual(ticks, 1,
        "A positive distance with positive speed must require at least one tick.");
    }

    [Test]
    public void ComputeTravelTicks_VerySmallPositiveDistance_ReturnsAtLeastOneTick() {
      // Distance that is tiny but clearly positive (1e-4 m) must ceil to ≥ 1.
      int ticks = SpellTravelLogic.ComputeTravelTicks(1e-4f, 20f, 60);
      Assert.GreaterOrEqual(ticks, 1,
        "Even a very small positive distance must produce at least one tick via ceil.");
    }

    [Test]
    public void ComputeTravelTicks_ZeroDistance_ReturnsZero() {
      // Implementation clamps d < 0 to 0 and returns 0 for d == 0.
      int ticks = SpellTravelLogic.ComputeTravelTicks(0f, 20f, 60);
      Assert.AreEqual(0, ticks,
        "Zero distance is treated as immediate — no travel ticks needed.");
    }

    [Test]
    public void ComputeTravelTicks_NearZeroNegativeDistance_ClampsToZero() {
      // Negative distance is clamped to 0, so result must also be 0.
      int ticks = SpellTravelLogic.ComputeTravelTicks(-1e-6f, 20f, 60);
      Assert.AreEqual(0, ticks,
        "Near-zero negative distance clamps to 0 and returns 0 ticks.");
    }

    // ── AdvanceMissilePosition — no overshoot ────────────────────────────

    [Test]
    public void AdvanceMissilePosition_StepLargerThanDistance_ClampsToTarget() {
      // Step is much larger than the remaining distance: missile must not overshoot.
      var missile = new Vector3(9f, 0f, 0f);
      var target  = new Vector3(10f, 0f, 0f); // 1 m away
      float speed = 500f;                      // very fast — step ≈ 8.33 m at 60 Hz
      float dt    = 1f / 60f;

      var result = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);

      Assert.AreEqual(target.x, result.x, 0.0001f,
        "Missile must not overshoot: result must be clamped to target position.");
      Assert.AreEqual(target.y, result.y, 0.0001f);
      Assert.AreEqual(target.z, result.z, 0.0001f);
    }

    [Test]
    public void AdvanceMissilePosition_ExactlyOneStepAway_LandsOnTarget() {
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = new Vector3(10f - step, 0f, 0f); // exactly one step away

      var result = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);

      Assert.AreEqual(target.x, result.x, 0.0001f,
        "Missile exactly one step away must land precisely on the target.");
    }

    // ── HasMissileArrived ────────────────────────────────────────────────

    [Test]
    public void HasMissileArrived_PostAdvance_WhenDistanceLessThanStep_ReturnsTrue() {
      // After advancing, missile is within step threshold — arrived.
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target   = new Vector3(10f, 0f, 0f);
      // Place missile so post-advance distance is half a step (< step).
      var preMissile = target - new Vector3(1.5f * step, 0f, 0f);
      var postMissile = SpellTravelLogic.AdvanceMissilePosition(preMissile, target, speed, dt);

      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(postMissile, target, speed, dt),
        "Post-advance missile within one step of target must register as arrived.");
    }

    [Test]
    public void HasMissileArrived_AtExactlyTarget_ReturnsTrue() {
      // Missile already sitting on the target: distance is 0, always < step.
      float speed = 20f;
      float dt    = 1f / 60f;
      var pos = new Vector3(5f, 3f, -2f);

      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(pos, pos, speed, dt),
        "A missile at exactly the target position must be considered arrived.");
    }

    [Test]
    public void HasMissileArrived_WhenDistanceWithinStep_ReturnsTrue() {
      // Strictly inside one step along X (implementation: distance < step * (1 + 1e-5)).
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = target - new Vector3(0.99f * step, 0f, 0f);

      float distance = Vector3.Distance(missile, target);
      Assert.Less(distance, step, "Setup must keep distance strictly below one step.");

      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(missile, target, speed, dt),
        "Missile strictly within one step of target must register as arrived.");
    }

    [Test]
    public void HasMissileArrived_WhenDistanceEqualsStep_ReturnsTrue() {
      // Boundary: d == step still satisfies d < step * (1 + 1e-5) in SpellTravelLogic.
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = target - new Vector3(step, 0f, 0f);

      float distance = Vector3.Distance(missile, target);
      Assert.AreEqual(step, distance, 1e-5f,
        "Setup: missile is exactly one simulation step from target along X.");

      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(missile, target, speed, dt),
        "At d == step, d < step*(1+1e-5) must hold so the arrival gate returns true.");
    }
  }
}
