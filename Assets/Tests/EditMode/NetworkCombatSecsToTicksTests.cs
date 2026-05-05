using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins <see cref="NetworkCombatController.SecsToTicks(int, float)"/> against
  /// Mathf.CeilToInt — timer math shared by cast timing and cooldown ticks.
  /// </summary>
  [TestFixture]
  public class NetworkCombatSecsToTicksTests {
    [Test]
    public void SecsToTicks_ZeroSeconds_ReturnsZero() {
      Assert.AreEqual(0, NetworkCombatController.SecsToTicks(60, 0f));
    }

    [Test]
    public void SecsToTicks_OneSecond_AtNormalTickRate_ReturnsFullSecondInTicks() {
      const int tickRate = 60;
      Assert.AreEqual(tickRate, NetworkCombatController.SecsToTicks(tickRate, 1f));
    }

    [Test]
    public void SecsToTicks_OnePointFiveSeconds_AtSixtyHz_IsNinetyTicks() {
      Assert.AreEqual(90, NetworkCombatController.SecsToTicks(60, 1.5f));
    }

    [Test]
    public void SecsToTicks_SmallPositiveSeconds_RoundsUpToOneTick_AtSixtyHz() {
      // Half a tick of wall time at 60 Hz: seconds * tickRate == 0.5f → ceil → 1
      Assert.AreEqual(1, NetworkCombatController.SecsToTicks(60, 1f / 120f));
    }

    [TestCase(60, 1.5f)]
    [TestCase(128, 1.55f)]
    [TestCase(30, 0f)]
    [TestCase(60, float.Epsilon)] // compile-time constant; Mathf.Epsilon is not (cannot be used in attributes)
    public void SecsToTicks_MatchesMathfCeilToInt(int tickRate, float seconds) {
      int expected = Mathf.CeilToInt(seconds * tickRate);
      Assert.AreEqual(
        expected,
        NetworkCombatController.SecsToTicks(tickRate, seconds),
        $"tickRate={tickRate} seconds={seconds}");
    }

    [Test]
    public void SecsToTicks_FireballAndHeavyBlastAt60Hz_AlignsWithSpellRegistry() {
      // Both Fireball and Heavy Blast have castTimeSec=2.5 → 150 ticks at 60 Hz.
      Assert.AreEqual(150, NetworkCombatController.SecsToTicks(60, 2.5f));
    }
  }
}
