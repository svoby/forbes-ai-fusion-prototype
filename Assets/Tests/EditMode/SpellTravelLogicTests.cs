using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  public class SpellTravelLogicTests {
    [Test]
    public void ComputeTravelTicks_NonPositiveSpeed_ReturnsZero() {
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(10f, 0f, 60));
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(10f, -5f, 60));
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(100f, 20f, 0));
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(100f, 20f, -10));
    }

    [Test]
    public void ComputeTravelTicks_ZeroDistance_ReturnsZero() {
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(0f, 20f, 60));
    }

    [Test]
    public void ComputeTravelTicks_NegativeDistance_ClampsLikeZero_ReturnsZero() {
      Assert.AreEqual(0, SpellTravelLogic.ComputeTravelTicks(-5f, 20f, 60));
    }

    [Test]
    public void ComputeTravelTicks_DistanceOverSpeed_UseCeilingTicks() {
      // 25m / 20 m/s @ 60 Hz = 1.25 * 60 = 75 ticks
      Assert.AreEqual(75, SpellTravelLogic.ComputeTravelTicks(25f, 20f, 60));
      // Slight overrun should ceil up
      Assert.AreEqual(76, SpellTravelLogic.ComputeTravelTicks(25.01f, 20f, 60));
    }

    [Test]
    public void ComputeImpactTick_IsReleasePlusTravel_And_TravelFloorsAtZero() {
      Assert.AreEqual(124, SpellTravelLogic.ComputeImpactTick(100, 24));
      Assert.AreEqual(100, SpellTravelLogic.ComputeImpactTick(100, -3));
    }

    [Test]
    public void HasProjectile_IsTrueOnlyWhenSpeedPositive() {
      var projectile = SpellRegistry.Get(1);
      var instant    = SpellRegistry.Get(2);
      Assert.IsTrue(SpellTravelLogic.HasProjectile(projectile));
      Assert.IsFalse(SpellTravelLogic.HasProjectile(instant));
    }

    // ── AdvanceMissilePosition ──────────────────────────────────────────

    [Test]
    public void AdvanceMissilePosition_MovesTowardTarget_ByStepDistance() {
      var missile = new Vector3(0f, 0f, 0f);
      var target  = new Vector3(10f, 0f, 0f);
      // speed 20 m/s, deltaTime 1/60 s → step = 1/3 m
      float speed = 20f;
      float dt    = 1f / 60f;

      var result = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);

      Assert.AreEqual(speed * dt, result.x, 0.0001f, "Missile should advance exactly one step.");
      Assert.AreEqual(0f, result.y, 0.0001f);
      Assert.AreEqual(0f, result.z, 0.0001f);
    }

    [Test]
    public void AdvanceMissilePosition_DoesNotOvershoot() {
      // Missile is already closer than one step — should clamp at target.
      var missile = new Vector3(9.99f, 0f, 0f);
      var target  = new Vector3(10f, 0f, 0f);
      float speed = 20f;
      float dt    = 1f / 60f; // step ≈ 0.333 m, distance = 0.01 m

      var result = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);

      Assert.AreEqual(target.x, result.x, 0.0001f, "Should clamp to target, not overshoot.");
    }

    [Test]
    public void AdvanceMissilePosition_ZeroSpeedOrDeltaTime_NoMovement() {
      var missile = new Vector3(5f, 0f, 0f);
      var target  = new Vector3(10f, 0f, 0f);

      var zeroSpeed = SpellTravelLogic.AdvanceMissilePosition(missile, target, 0f, 1f / 60f);
      var zeroDt    = SpellTravelLogic.AdvanceMissilePosition(missile, target, 20f, 0f);
      var negSpeed  = SpellTravelLogic.AdvanceMissilePosition(missile, target, -5f, 1f / 60f);

      Assert.AreEqual(missile, zeroSpeed, "Zero speed must not move missile.");
      Assert.AreEqual(missile, zeroDt,    "Zero deltaTime must not move missile.");
      Assert.AreEqual(missile, negSpeed,  "Negative speed must not move missile.");
    }

    // ── HasMissileArrived ───────────────────────────────────────────────

    [Test]
    public void HasMissileArrived_WhenWithinStep_ReturnsTrue() {
      // Missile is 0.1 m from target; step is 0.333 m → arrived.
      var missile = new Vector3(9.9f, 0f, 0f);
      var target  = new Vector3(10f,  0f, 0f);
      float speed = 20f;
      float dt    = 1f / 60f;

      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(missile, target, speed, dt));
    }

    [Test]
    public void HasMissileArrived_WhenBeyondStep_ReturnsFalse() {
      // Missile is 5 m from target; step is 0.333 m → not arrived.
      var missile = new Vector3(5f,  0f, 0f);
      var target  = new Vector3(10f, 0f, 0f);
      float speed = 20f;
      float dt    = 1f / 60f;

      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(missile, target, speed, dt));
    }

    [Test]
    public void HasMissileArrived_ZeroSpeed_NeverArrives() {
      // Even if missile is exactly at target, zero speed means no valid step.
      var pos = new Vector3(10f, 0f, 0f);

      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(pos, pos, 0f,   1f / 60f));
      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(pos, pos, 20f,  0f));
      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(pos, pos, -5f,  1f / 60f));
    }

    // ── Advance-then-check production pattern ───────────────────────────────
    // ActiveSpellInstanceRegistry.TickInstance calls AdvanceMissilePosition
    // and then HasMissileArrived with the POST-ADVANCE position.  These tests
    // document the actual arrival semantics used in production: a missile between
    // 1× and 2× one tick's step from the target is detected as arrived on the
    // same tick it advances (post-advance distance falls within the step threshold).

    [Test]
    public void AdvanceThenCheck_MissileAtOnePlusStep_ArrivesOnSameTick() {
      // Missile at exactly 1.5 × step from target.
      // Pre-advance: NOT arrived (1.5 steps > threshold 1 step).
      // Post-advance: arrived (moves 1 step → 0.5 steps from target ≤ threshold).
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = target - new Vector3(1.5f * step, 0f, 0f);

      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(missile, target, speed, dt),
        "Pre-advance: 1.5 steps away should not register as arrived.");

      var advanced = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);
      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(advanced, target, speed, dt),
        "Post-advance (production pattern): missile at 1.5 steps arrives on the same tick.");
    }

    [Test]
    public void AdvanceThenCheck_MissileAtMoreThanTwoSteps_DoesNotArriveSameTick() {
      // Missile at 2.1 × step: after advancing by 1 step it is still 1.1 steps
      // away, which exceeds the threshold — impact is deferred to the next tick.
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = target - new Vector3(2.1f * step, 0f, 0f);

      var advanced = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);
      Assert.IsFalse(SpellTravelLogic.HasMissileArrived(advanced, target, speed, dt),
        "Post-advance: 2.1 steps means post-advance distance is 1.1 steps, still above threshold.");
    }

    [Test]
    public void AdvanceThenCheck_MissileAtExactlyTwoSteps_ArrivesSameTick() {
      // Missile at exactly 2 steps: advances to exactly 1 step → arrived.
      float speed = 20f;
      float dt    = 1f / 60f;
      float step  = speed * dt;

      var target  = new Vector3(10f, 0f, 0f);
      var missile = target - new Vector3(2f * step, 0f, 0f);

      var advanced = SpellTravelLogic.AdvanceMissilePosition(missile, target, speed, dt);
      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(advanced, target, speed, dt),
        "Post-advance: exactly 2 steps → post-advance distance equals threshold → arrived.");
    }

    [Test]
    public void AdvanceThenCheck_MissileAtTarget_ArrivesSameTick() {
      // Edge case: missile already at target (distance 0). Advance is a no-op;
      // HasMissileArrived returns true because 0 ≤ step.
      float speed = 20f;
      float dt    = 1f / 60f;

      var pos = new Vector3(10f, 0f, 0f);

      var advanced = SpellTravelLogic.AdvanceMissilePosition(pos, pos, speed, dt);
      Assert.AreEqual(pos, advanced, "No movement when already at target.");
      Assert.IsTrue(SpellTravelLogic.HasMissileArrived(advanced, pos, speed, dt),
        "Missile already at target must arrive immediately.");
    }
  }
}
