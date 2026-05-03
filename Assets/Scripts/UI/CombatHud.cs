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
///   <item>Last cast failure reason (shown for 2 s)</item>
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
  string _failLine   = "";

  void Awake() {
    _runner    = GetComponent<NetworkRunner>();
    _targeting = GetComponent<TargetingController>();
  }

  void Update() {
    if (_runner == null || !_runner.IsRunning) {
      _selfLine = _targetLine = _gcdLine = _cdLine = _failLine = "";
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
    _gcdLine = _cdLine = _failLine = "";

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

    // Fail reason: show for ~2 seconds after it was set.
    if (combat.LastFailTick > 0 && combat.LastFailReason != 0) {
      float ageSecs = TicksToSecs(_runner.Tick - combat.LastFailTick);
      if (ageSecs < 2f) {
        _failLine = $"! {(CombatFailReason)combat.LastFailReason}";
      }
    }
  }

  void OnGUI() {
    const float pad  = 12f;
    const float rowH = 22f;

    var style = new GUIStyle(GUI.skin.label) {
      fontSize = 15,
      normal   = { textColor = Color.white },
    };

    var failStyle = new GUIStyle(style) {
      normal = { textColor = new Color(1f, 0.3f, 0.3f) },
    };

    float y = pad;
    DrawLine(_selfLine,   style,    pad, ref y, rowH);
    DrawLine(_targetLine, style,    pad, ref y, rowH);
    DrawLine(_gcdLine,    style,    pad, ref y, rowH);
    DrawLine(_cdLine,     style,    pad, ref y, rowH);
    DrawLine(_failLine,   failStyle, pad, ref y, rowH);
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
