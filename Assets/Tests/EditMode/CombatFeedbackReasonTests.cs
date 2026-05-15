using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Guards <see cref="CombatFeedbackReason"/> names and byte layout used by HUD / replication.
  /// Values 0–7 align with <see cref="CombatFailReason"/> per <c>CombatFeedbackReason.cs</c> header.
  /// </summary>
  [TestFixture]
  public class CombatFeedbackReasonTests {
    [TestCase(CombatFeedbackReason.None, (byte)0)]
    [TestCase(CombatFeedbackReason.NoTarget, (byte)1)]
    [TestCase(CombatFeedbackReason.OutOfRange, (byte)2)]
    [TestCase(CombatFeedbackReason.TargetDead, (byte)3)]
    [TestCase(CombatFeedbackReason.OnCooldown, (byte)4)]
    [TestCase(CombatFeedbackReason.GcdActive, (byte)5)]
    [TestCase(CombatFeedbackReason.AlreadyCasting, (byte)6)]
    [TestCase(CombatFeedbackReason.CasterDead, (byte)7)]
    public void Enum_ByteMapping_ZeroThroughSeven_MatchesValidatorWireSlots(CombatFeedbackReason value, byte expectedByte) {
      Assert.AreEqual(expectedByte, (byte)value,
        $"CombatFeedbackReason.{value} byte value drifted from {expectedByte}; breaks alignment with CombatFailReason / wire.");
    }

    [TestCase(CombatFeedbackReason.CastInterruptedByMovement, (byte)8)]
    [TestCase(CombatFeedbackReason.CastInterruptedByJump, (byte)9)]
    [TestCase(CombatFeedbackReason.CastInterruptedByNewSpell, (byte)10)]
    [TestCase(CombatFeedbackReason.CastInterruptedByDeath, (byte)11)]
    [TestCase(CombatFeedbackReason.CastInterruptedInvalidTarget, (byte)12)]
    [TestCase(CombatFeedbackReason.CastInterruptedManual, (byte)13)]
    public void Enum_ByteMapping_CastInterruptSlots_AreStable(CombatFeedbackReason value, byte expectedByte) {
      Assert.AreEqual(expectedByte, (byte)value,
        $"CombatFeedbackReason.{value} byte value drifted from {expectedByte}.");
    }

    [Test]
    public void None_IsNumericZero() {
      Assert.AreEqual(0, (int)CombatFeedbackReason.None);
    }

    [Test]
    public void Enum_ContainsExpectedMemberNames_FromProductionEnum() {
      var names = new System.Collections.Generic.HashSet<string>(System.Enum.GetNames(typeof(CombatFeedbackReason)));
      foreach (var expected in new[] {
               "None",
               "NoTarget",
               "OutOfRange",
               "TargetDead",
               "OnCooldown",
               "GcdActive",
               "AlreadyCasting",
               "CasterDead",
               "CastInterruptedByMovement",
               "CastInterruptedByJump",
               "CastInterruptedByNewSpell",
               "CastInterruptedByDeath",
               "CastInterruptedInvalidTarget",
               "CastInterruptedManual",
             }) {
        Assert.IsTrue(names.Contains(expected), $"Missing enum member name: {expected}");
      }
    }
  }
}
