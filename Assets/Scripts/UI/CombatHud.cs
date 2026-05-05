using Fusion;
using UnityEngine;

/// <summary>
/// Local debug HUD. Reads networked state from the local player's objects and from
/// the sibling <see cref="TargetingController"/>. No gameplay logic lives here.
/// <para>
/// Displayed info:
/// <list type="bullet">
///   <item>Own HP</item>
///   <item>Selected target name + HP</item>
///   <item>Cast timing is shown by <see cref="CastBarView"/> (canvas)</item>
///   <item>GCD remaining</item>
///   <item>Per-spell cooldowns [1] [2] [3]</item>
///   <item>Combat feedback banner (WoW-style, upper golden-ratio; ~2 s)</item>
/// </list>
/// </para>
/// </summary>
public class CombatHud : MonoBehaviour {
  NetworkRunner       _runner;
  TargetingController _targeting;

  string _selfLine   = "";
  string _targetLine = "";
  string _gcdLine    = "";
  string _cdLine     = "";
  string _feedbackLine = "";

  void Awake() {
    _runner    = GetComponent<NetworkRunner>();
    _targeting = GetComponent<TargetingController>();
  }

  void Update() {
    if (_runner == null || !_runner.IsRunning) {
      _selfLine = _targetLine = _gcdLine = _cdLine = _feedbackLine = "";
      return;
    }

    UpdateSelfLine();
    UpdateTargetLine();
    UpdateCombatLines();
  }

  void UpdateSelfLine() {
    if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj) ||
        !playerObj.TryGetComponent(out Health self)) {
      _selfLine = "HP: —";
      return;
    }

    _selfLine = $"HP: {self.NetworkedHealth:0}/{self.StartingHealth:0}  (1/2/3=spell  E=color)";
  }

  void UpdateTargetLine() {
    var t = _targeting != null ? _targeting.CurrentTarget : null;
    if (t == null) {
      _targetLine = "Target: none";
      return;
    }

    string hp = "";
    if (t.TryGetComponent(out Health th)) {
      hp = $"  HP: {th.NetworkedHealth:0}/{th.StartingHealth:0}";
    }

    _targetLine = $"Target: {t.DisplayName}{hp}";
  }

  void UpdateCombatLines() {
    _gcdLine = _cdLine = _feedbackLine = "";

    if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj)) {
      return;
    }

    if (!playerObj.TryGetComponent(out NetworkCombatController combat)) {
      return;
    }

    // GCD.
    float gcdRemain = TicksToSecs(combat.GcdEndTick - _runner.Tick);
    if (gcdRemain > 0f) {
      _gcdLine = $"GCD: {gcdRemain:0.0}s";
    }

    // Per-spell cooldowns.
    float cd1 = TicksToSecs(combat.Cooldown1EndTick - _runner.Tick);
    float cd2 = TicksToSecs(combat.Cooldown2EndTick - _runner.Tick);
    float cd3 = TicksToSecs(combat.Cooldown3EndTick - _runner.Tick);
    _cdLine = $"CD: [1] {FmtCd(cd1)}  [2] {FmtCd(cd2)}  [3] {FmtCd(cd3)}";

    var feedback = (CombatFeedbackReason)combat.LastCombatFeedbackReason;
    if (IsFeedbackLineVisible(feedback, combat.LastCombatFeedbackTick, _runner.Tick, _runner.DeltaTime)
        && ShouldShowCombatFeedbackInBanner(feedback)) {
      _feedbackLine = $"! {feedback}";
    }
  }

  /// <summary>
  /// Reasons still replicated on <see cref="NetworkCombatController"/> but not shown as the centered banner
  /// (players already have GCD line; caster-death is not a cast “error” in the WoW sense).
  /// </summary>
  internal static bool ShouldShowCombatFeedbackInBanner(CombatFeedbackReason reason) {
    return reason != CombatFeedbackReason.None
           && reason != CombatFeedbackReason.GcdActive
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

  void OnGUI() {
    const float pad  = 12f;
    const float rowH = 22f;

    var style = new GUIStyle(GUI.skin.label) {
      fontSize = 15,
      normal   = { textColor = Color.white },
    };

    float y = pad;
    DrawLine(_selfLine,   style, pad, ref y, rowH);
    DrawLine(_targetLine, style, pad, ref y, rowH);
    DrawLine(_gcdLine,    style, pad, ref y, rowH);
    DrawLine(_cdLine,     style, pad, ref y, rowH);

    DrawCombatFeedbackBanner();
  }

  /// <summary>
  /// WoW-like error line: horizontally centered, vertical position at φ⁻² · height from top (~38 %).
  /// </summary>
  void DrawCombatFeedbackBanner() {
    if (string.IsNullOrEmpty(_feedbackLine)) {
      return;
    }

    const float phi = 1.618033988749f;
    // First golden-section cut measured from top of view (classic 1/φ² ≈ .382).
    float yCenter = Screen.height / (phi * phi);

    var textStyle = new GUIStyle(GUI.skin.label) {
      alignment = TextAnchor.MiddleCenter,
      fontSize  = 20,
      fontStyle = FontStyle.Bold,
      normal    = { textColor = new Color(1f, 0.25f, 0.2f) },
    };

    const float bandH = 56f;
    var rect = new Rect(0f, yCenter - bandH * 0.5f, Screen.width, bandH);

    var shadowStyle = new GUIStyle(textStyle) {
      normal = { textColor = new Color(0f, 0f, 0f, 0.82f) },
    };

    const float sh = 2f;
    GUI.Label(new Rect(rect.x + sh, rect.y + sh, rect.width, rect.height), _feedbackLine, shadowStyle);
    GUI.Label(rect, _feedbackLine, textStyle);
  }

  static void DrawLine(string text, GUIStyle style, float x, ref float y, float rowH) {
    if (string.IsNullOrEmpty(text)) {
      return;
    }
    GUI.Label(new Rect(x, y, 900f, rowH), text, style);
    y += rowH;
  }

  float TicksToSecs(int ticks) {
    if (ticks <= 0) {
      return 0f;
    }
    return ticks * _runner.DeltaTime;
  }

  static string FmtCd(float secs) => secs > 0f ? $"{secs:0.0}s" : "ready";
}
