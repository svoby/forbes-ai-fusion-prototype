using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas-based WoW-style combat warning banner for the local player.
/// Horizontally centered, vertically at the golden-ratio position (~38 % from top).
/// Visible for ~2 s when a player-facing combat rejection or cast interrupt fires.
/// <para>
/// Presentation-only: reads replicated fields from the local player's
/// <see cref="NetworkCombatController"/>. No gameplay logic.
/// </para>
/// <para>
/// Built and wired by <see cref="RuntimeHudBootstrap.EnsureForRunner"/> at runtime, and by
/// <c>ForbesFusionSharedSceneSetup.EnsureHudCanvas</c> in the Editor.
/// </para>
/// </summary>
public class CombatFeedbackBannerView : MonoBehaviour {
  [SerializeField] CanvasGroup _group;
  [SerializeField] Text        _label;

  NetworkRunner _runner;

  /// <summary>Wires UI references built in code or from the editor setup tool.</summary>
  public void BindUi(CanvasGroup group, Text label) {
    _group = group;
    _label = label;
  }

  void Awake() {
    _runner = GetComponentInParent<NetworkRunner>();
    Hide();
  }

  void Update() {
    if (_runner == null || !_runner.IsRunning) {
      Hide();
      return;
    }

    if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj)
        || !playerObj.TryGetComponent(out NetworkCombatController combat)) {
      Hide();
      return;
    }

    var reason = (CombatFeedbackReason)combat.LastCombatFeedbackReason;
    if (IsFeedbackLineVisible(reason, combat.LastCombatFeedbackTick, _runner.Tick, _runner.DeltaTime)
        && ShouldShowCombatFeedbackInBanner(reason)) {
      Show(CombatWarningText.ForReason(reason));
    } else {
      Hide();
    }
  }

  void Show(string text) {
    if (_group != null) _group.alpha = 1f;
    if (_label != null) _label.text  = text;
  }

  void Hide() {
    if (_group != null) _group.alpha = 0f;
    if (_label != null) _label.text  = "";
  }

  // ── Static helpers (EditMode-testable, no MonoBehaviour dependency) ───────────

  /// <summary>
  /// Reasons not shown as the centered banner:
  /// caster-death is not a cast "error" in the WoW sense;
  /// NewSpell interruption is silent from the player's perspective.
  /// </summary>
  internal static bool ShouldShowCombatFeedbackInBanner(CombatFeedbackReason reason) {
    return reason != CombatFeedbackReason.None
           && reason != CombatFeedbackReason.CasterDead
           && reason != CombatFeedbackReason.CastInterruptedByNewSpell;
  }

  /// <summary>EditMode-testable mirror of the time-window rule used with <see cref="ShouldShowCombatFeedbackInBanner"/>.</summary>
  internal static bool IsFeedbackLineVisible(
      CombatFeedbackReason reason,
      int feedbackTick,
      int currentRunnerTick,
      float runnerDeltaTime,
      float visibleDurationSecs = 2f) {
    if (feedbackTick <= 0 || reason == CombatFeedbackReason.None) {
      return false;
    }
    int ageTicks = currentRunnerTick - feedbackTick;
    if (ageTicks < 0) {
      return false;
    }
    float ageSecs = ageTicks * runnerDeltaTime;
    return ageSecs < visibleDurationSecs;
  }
}
