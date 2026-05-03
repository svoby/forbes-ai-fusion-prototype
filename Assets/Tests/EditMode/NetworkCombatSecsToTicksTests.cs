using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins <see cref="NetworkCombatController.SecsToTicks(int, float)"/> against
  /// Mathf.CeilToInt — timer math shared by cast timing and cooldown ticks.
  /// </summary>
  [TestFixture]
  public class NetworkCombatSecsToTicksTests {
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
    public void SecsToTicks_FireballAt60Hz_AlignsWithSpellRegistry() {
      Assert.AreEqual(90, NetworkCombatController.SecsToTicks(60, 1.5f));
      Assert.AreEqual(150, NetworkCombatController.SecsToTicks(60, 2.5f));
    }
  }
}
