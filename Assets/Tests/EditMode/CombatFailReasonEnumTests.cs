using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins the byte values of <see cref="CombatFailReason"/>. These match
  /// <see cref="CombatFeedbackReason"/> values 0–7 and replicate as
  /// <c>NetworkCombatController.LastCombatFeedbackReason</c>; renumbering would
  /// silently corrupt every running session. New values must only be appended.
  /// </summary>
  [TestFixture]
  public class CombatFailReasonEnumTests {
    [TestCase(CombatFailReason.None,           (byte)0)]
    [TestCase(CombatFailReason.NoTarget,       (byte)1)]
    [TestCase(CombatFailReason.OutOfRange,     (byte)2)]
    [TestCase(CombatFailReason.TargetDead,     (byte)3)]
    [TestCase(CombatFailReason.OnCooldown,     (byte)4)]
    [TestCase(CombatFailReason.GcdActive,      (byte)5)]
    [TestCase(CombatFailReason.AlreadyCasting, (byte)6)]
    [TestCase(CombatFailReason.CasterDead,     (byte)7)]
    public void Enum_ByteMapping_IsStable(CombatFailReason value, byte expectedByte) {
      Assert.AreEqual(expectedByte, (byte)value,
        $"CombatFailReason.{value} byte value drifted from {expectedByte}; this breaks wire compatibility.");
    }
  }
}
