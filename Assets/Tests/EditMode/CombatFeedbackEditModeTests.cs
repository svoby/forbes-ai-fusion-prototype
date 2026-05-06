using System;
using System.Linq;
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
  public class CombatFeedbackBannerVisibilityTests {
    [Test]
    public void IsFeedbackLineVisible_True_WhenWithinTwoSeconds() {
      float dt = 1f / 60f;
      Assert.IsTrue(CombatFeedbackBannerView.IsFeedbackLineVisible(
        CombatFeedbackReason.NoTarget,
        feedbackTick: 1000,
        currentRunnerTick: 1000 + 60, // 1s
        dt,
        visibleDurationSecs: 2f));
    }

    [TestCase(CombatFeedbackReason.None,                      false)]
    [TestCase(CombatFeedbackReason.GcdActive,                  true)]
    [TestCase(CombatFeedbackReason.CasterDead,                 false)]
    [TestCase(CombatFeedbackReason.CastInterruptedByNewSpell,  false)]
    [TestCase(CombatFeedbackReason.NoTarget,                   true)]
    [TestCase(CombatFeedbackReason.OutOfRange,                 true)]
    [TestCase(CombatFeedbackReason.TargetDead,                 true)]
    [TestCase(CombatFeedbackReason.OnCooldown,                 true)]
    [TestCase(CombatFeedbackReason.CastInterruptedByDeath,     true)]
    [TestCase(CombatFeedbackReason.CastInterruptedInvalidTarget, true)]
    public void ShouldShowCombatFeedbackInBanner_Matches(CombatFeedbackReason reason, bool expected) {
      Assert.AreEqual(expected, CombatFeedbackBannerView.ShouldShowCombatFeedbackInBanner(reason));
    }

    [Test]
    public void IsFeedbackLineVisible_False_WhenOlderThanTwoSeconds() {
      float dt = 1f / 60f;
      Assert.IsFalse(CombatFeedbackBannerView.IsFeedbackLineVisible(
        CombatFeedbackReason.NoTarget,
        feedbackTick: 1000,
        currentRunnerTick: 1000 + 200, // 200/60 > 2s
        dt,
        visibleDurationSecs: 2f));
    }

    [Test]
    public void IsFeedbackLineVisible_False_ForNoneOrNonPositiveTick() {
      Assert.IsFalse(CombatFeedbackBannerView.IsFeedbackLineVisible(
        CombatFeedbackReason.None,
        feedbackTick: 10,
        currentRunnerTick: 20,
        1f / 60f));
      Assert.IsFalse(CombatFeedbackBannerView.IsFeedbackLineVisible(
        CombatFeedbackReason.OutOfRange,
        feedbackTick: 0,
        currentRunnerTick: 20,
        1f / 60f));
    }
  }

  /// <summary>
  /// Pins the mid-cast cancel policy in
  /// <see cref="NetworkCombatController.IsMidCastCancelReason"/>.
  ///
  /// The rule: <see cref="CombatFailReason.OutOfRange"/> must NOT cancel an
  /// active cast — the target may re-enter range before the cast resolves.
  /// All other non-None failures (NoTarget, TargetDead, …) must cancel.
  /// </summary>
  [TestFixture]
  public class MidCastCancelPolicyTests {
    [Test]
    public void OutOfRange_DoesNotCancelMidCast() {
      Assert.IsFalse(
        NetworkCombatController.IsMidCastCancelReason(CombatFailReason.OutOfRange),
        "OutOfRange must not interrupt a cast in progress: target may walk back.");
    }

    [Test]
    public void None_DoesNotCancelMidCast() {
      Assert.IsFalse(
        NetworkCombatController.IsMidCastCancelReason(CombatFailReason.None),
        "None is a success path and must not trigger a cancel.");
    }

    [TestCase(CombatFailReason.NoTarget)]
    [TestCase(CombatFailReason.TargetDead)]
    [TestCase(CombatFailReason.OnCooldown)]
    [TestCase(CombatFailReason.GcdActive)]
    [TestCase(CombatFailReason.AlreadyCasting)]
    [TestCase(CombatFailReason.CasterDead)]
    public void OtherFailReasons_DoCancelMidCast(CombatFailReason reason) {
      Assert.IsTrue(
        NetworkCombatController.IsMidCastCancelReason(reason),
        $"{reason} should cancel an in-progress cast immediately.");
    }

    [Test]
    public void AllEnumValues_AreCoveredByPolicy() {
      // Guard: if a new CombatFailReason is added, this test fails until someone
      // explicitly decides whether it should or should not cancel mid-cast and
      // updates IsMidCastCancelReason accordingly.
      var nonCancelling = new[] { CombatFailReason.None, CombatFailReason.OutOfRange };
      var allValues = (CombatFailReason[])Enum.GetValues(typeof(CombatFailReason));

      foreach (var reason in allValues) {
        bool expectCancel = !nonCancelling.Contains(reason);
        Assert.AreEqual(
          expectCancel,
          NetworkCombatController.IsMidCastCancelReason(reason),
          $"Policy not defined for {reason} — update IsMidCastCancelReason or this test.");
      }
    }
  }
}
