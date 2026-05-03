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
      Assert.AreEqual(100, SpellTravelLogic.ComputeImpactTick(100, 24));
      Assert.AreEqual(100, SpellTravelLogic.ComputeImpactTick(100, -3));
    }

    [Test]
    public void HasProjectile_IsTrueOnlyWhenSpeedPositive() {
      var projectile = SpellRegistry.Get(1);
      var instant    = SpellRegistry.Get(2);
      Assert.IsTrue(SpellTravelLogic.HasProjectile(projectile));
      Assert.IsFalse(SpellTravelLogic.HasProjectile(instant));
    }
  }
}
