using System;
using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins appended <see cref="CombatFeedbackReason"/> wire values (8+). Values 0–7 are
  /// covered by <see cref="CombatFailReasonEnumTests"/>.
  /// </summary>
  [TestFixture]
  public class CombatFeedbackReasonEnumTests {
    [TestCase(CombatFeedbackReason.CastInterruptedByMovement,    (byte)8)]
    [TestCase(CombatFeedbackReason.CastInterruptedByJump,       (byte)9)]
    [TestCase(CombatFeedbackReason.CastInterruptedByNewSpell,   (byte)10)]
    [TestCase(CombatFeedbackReason.CastInterruptedByDeath,     (byte)11)]
    [TestCase(CombatFeedbackReason.CastInterruptedInvalidTarget, (byte)12)]
    [TestCase(CombatFeedbackReason.CastInterruptedManual,       (byte)13)]
    public void InterruptReason_ByteMapping_IsStable(CombatFeedbackReason value, byte expectedByte) {
      Assert.AreEqual(expectedByte, (byte)value,
        $"CombatFeedbackReason.{value} byte value drifted from {expectedByte}; this breaks wire compatibility.");
    }
  }

  [TestFixture]
  public class CombatFeedbackMappingTests {
    [Test]
    public void FromValidatorFailure_MatchesCombatFailReasonBytes() {
      foreach (CombatFailReason fr in Enum.GetValues(typeof(CombatFailReason))) {
        var fb = CombatFeedbackReasonMapping.FromValidatorFailure(fr);
        Assert.AreEqual((byte)fr, (byte)fb,
          $"Byte for {fr} -> {fb} must match validator/HUD parity.");
      }
    }

    [TestCase(CastCancelReason.None,           CombatFeedbackReason.None)]
    [TestCase(CastCancelReason.Movement,      CombatFeedbackReason.None)]
    [TestCase(CastCancelReason.Jump,          CombatFeedbackReason.None)]
    [TestCase(CastCancelReason.NewSpell,      CombatFeedbackReason.CastInterruptedByNewSpell)]
    [TestCase(CastCancelReason.Death,         CombatFeedbackReason.CastInterruptedByDeath)]
    [TestCase(CastCancelReason.InvalidTarget, CombatFeedbackReason.CastInterruptedInvalidTarget)]
    [TestCase(CastCancelReason.Manual,        CombatFeedbackReason.CastInterruptedManual)]
    public void FromCastCancel_Maps(CastCancelReason cancel, CombatFeedbackReason expected) {
      Assert.AreEqual(expected, CombatFeedbackReasonMapping.FromCastCancel(cancel));
    }
  }

  [TestFixture]
  public class CombatHudFeedbackVisibilityTests {
    [Test]
    public void IsFeedbackLineVisible_True_WhenWithinTwoSeconds() {
      float dt = 1f / 60f;
      Assert.IsTrue(CombatHud.IsFeedbackLineVisible(
        CombatFeedbackReason.NoTarget,
        feedbackTick: 1000,
        currentRunnerTick: 1000 + 60, // 1s
        dt,
        visibleDurationSecs: 2f));
    }

    [TestCase(CombatFeedbackReason.None,                      false)]
    [TestCase(CombatFeedbackReason.GcdActive,                  false)]
    [TestCase(CombatFeedbackReason.CasterDead,                 false)]
    [TestCase(CombatFeedbackReason.CastInterruptedByNewSpell,  false)]
    [TestCase(CombatFeedbackReason.NoTarget,                   true)]
    [TestCase(CombatFeedbackReason.OutOfRange,                 true)]
    [TestCase(CombatFeedbackReason.TargetDead,                 true)]
    [TestCase(CombatFeedbackReason.OnCooldown,                 true)]
    [TestCase(CombatFeedbackReason.CastInterruptedByDeath,     true)]
    [TestCase(CombatFeedbackReason.CastInterruptedInvalidTarget, true)]
    public void ShouldShowCombatFeedbackInBanner_Matches(CombatFeedbackReason reason, bool expected) {
      Assert.AreEqual(expected, CombatHud.ShouldShowCombatFeedbackInBanner(reason));
    }

    [Test]
    public void IsFeedbackLineVisible_False_WhenOlderThanTwoSeconds() {
      float dt = 1f / 60f;
      Assert.IsFalse(CombatHud.IsFeedbackLineVisible(
        CombatFeedbackReason.NoTarget,
        feedbackTick: 1000,
        currentRunnerTick: 1000 + 200, // 200/60 > 2s
        dt,
        visibleDurationSecs: 2f));
    }

    [Test]
    public void IsFeedbackLineVisible_False_ForNoneOrNonPositiveTick() {
      Assert.IsFalse(CombatHud.IsFeedbackLineVisible(
        CombatFeedbackReason.None,
        feedbackTick: 10,
        currentRunnerTick: 20,
        1f / 60f));
      Assert.IsFalse(CombatHud.IsFeedbackLineVisible(
        CombatFeedbackReason.OutOfRange,
        feedbackTick: 0,
        currentRunnerTick: 20,
        1f / 60f));
    }
  }
}
