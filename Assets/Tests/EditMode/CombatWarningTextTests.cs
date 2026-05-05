using System;
using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins <see cref="CombatWarningText.ForReason"/> mapping: no runner, no scene.
  /// </summary>
  [TestFixture]
  public class CombatWarningTextTests {
    // ── Key player-facing messages ────────────────────────────────────────────

    [TestCase(CombatFeedbackReason.NoTarget,                     "No target")]
    [TestCase(CombatFeedbackReason.OutOfRange,                   "Out of range")]
    [TestCase(CombatFeedbackReason.TargetDead,                   "Target is dead")]
    [TestCase(CombatFeedbackReason.OnCooldown,                   "Spell is on cooldown")]
    [TestCase(CombatFeedbackReason.AlreadyCasting,               "Already casting")]
    [TestCase(CombatFeedbackReason.CastInterruptedByDeath,       "Interrupted: you died")]
    [TestCase(CombatFeedbackReason.CastInterruptedInvalidTarget, "Interrupted: target lost")]
    public void ForReason_BannerReasons_ReturnNonEmptyPlayerText(
        CombatFeedbackReason reason, string expectedText) {
      Assert.AreEqual(expectedText, CombatWarningText.ForReason(reason),
        $"CombatWarningText.ForReason({reason}) returned unexpected text.");
    }

    // ── Silent / suppressed reasons return empty ──────────────────────────────

    [TestCase(CombatFeedbackReason.None)]
    [TestCase(CombatFeedbackReason.CastInterruptedByMovement)]
    [TestCase(CombatFeedbackReason.CastInterruptedByJump)]
    public void ForReason_SilentReasons_ReturnEmptyString(CombatFeedbackReason reason) {
      Assert.AreEqual("", CombatWarningText.ForReason(reason),
        $"CombatWarningText.ForReason({reason}) must return empty string (silent cancel).");
    }

    // ── Coverage guard: every defined value returns non-null ─────────────────

    [Test]
    public void ForReason_AllDefinedValues_ReturnNonNull() {
      foreach (CombatFeedbackReason reason in Enum.GetValues(typeof(CombatFeedbackReason))) {
        string text = CombatWarningText.ForReason(reason);
        Assert.IsNotNull(text,
          $"CombatWarningText.ForReason({reason}) must never return null.");
      }
    }

    // ── Banner-visible reasons must not be empty ──────────────────────────────
    // Reasons that pass ShouldShowCombatFeedbackInBanner must map to a non-empty
    // string so the banner actually displays something meaningful.

    [Test]
    public void ForReason_BannerVisibleReasons_ReturnNonEmptyString() {
      foreach (CombatFeedbackReason reason in Enum.GetValues(typeof(CombatFeedbackReason))) {
        if (!CombatHud.ShouldShowCombatFeedbackInBanner(reason)) {
          continue;
        }
        string text = CombatWarningText.ForReason(reason);
        Assert.IsFalse(string.IsNullOrEmpty(text),
          $"CombatWarningText.ForReason({reason}) is empty but the reason " +
          $"passes ShouldShowCombatFeedbackInBanner — banner would display nothing.");
      }
    }
  }
}
