using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WoW-like cast bar for the local player's <see cref="NetworkCombatController"/>.
/// Presentation-only: reads networked cast ticks and hides for instant casts.
/// </summary>
public class CastBarView : MonoBehaviour {
  public const float CastBarPanelWidth   = 640f;
  public const float CastBarPanelHeight = 112f;

  /// <summary>Pixels above screen bottom for bottom-anchored cast bar pivot (WoW-like: above thumb / action-bar zone).</summary>
  public const float CastBarLiftFromBottomPx = 172f;

  [SerializeField] CanvasGroup _rootGroup;

  /// <summary>Horizontal fill (<see cref="Image.Type.Filled"/>).</summary>
  [SerializeField] Image _fillImage;

  [SerializeField] Text _spellNameText;

  [SerializeField, Tooltip("Lerp speed for smoothing fill presentation only.")]
  float _fillSmoothSpeed = 12f;

  NetworkRunner _runner;
  float _displayFill;

  /// <summary>True when editor or <see cref="BindUi"/> wired the bar.</summary>
  public bool IsUiBound => _rootGroup != null && _fillImage != null;

  /// <summary>Wires UI built in code or from the editor setup tool.</summary>
  public void BindUi(CanvasGroup rootGroup, Image fillImage, Text spellNameText) {
    _rootGroup      = rootGroup;
    _fillImage      = fillImage;
    _spellNameText  = spellNameText;
  }

  public const string BundledHudFontResourcesPath = "ForbesHud/NotoSans-Regular";

  /// <summary>
  /// Ships with OFL-licensed Noto Sans under <c>Assets/Resources/ForbesHud/</c> so UGUI scales without relying on Unity built-ins or OS fonts.
  /// </summary>
  public static Font ResolveDefaultHudFont(int sizeForDynamic = 26) {
    Font f = Resources.Load<Font>(BundledHudFontResourcesPath);
    if (f != null) {
      return f;
    }

    f = Resources.GetBuiltinResource<Font>("Arial.ttf");
    if (f != null) {
      return f;
    }
    f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    if (f != null) {
      return f;
    }
    foreach (var name in new[] {
               "Segoe UI", "Arial", "Liberation Sans", "Helvetica Neue", "DejaVu Sans", "Sans-serif",
             }) {
      try {
        f = Font.CreateDynamicFontFromOSFont(name, sizeForDynamic);
        if (f != null) {
          return f;
        }
      }
      catch {
        // Try next candidate.
      }
    }
    ForbesLog.Warn(
      "[CastBarView] No UI font resolved — Resources load path '" + BundledHudFontResourcesPath
      + "' missing or failed import; built-in Arial/Legacy absent; OS fallbacks failed. Text may not render.");
    return null;
  }

  /// <summary>Shared typography for UGUI.Text on the HUD (editor runtime builder + play mode).</summary>
  public static void StyleHudText(
    Text               t,
    Font               font,
    int                size,
    FontStyle          style,
    Color              color,
    HorizontalWrapMode horizontal = HorizontalWrapMode.Overflow,
    VerticalWrapMode   vertical   = VerticalWrapMode.Truncate) {
    if (font != null) {
      t.font = font;
    }
    t.fontStyle           = style;
    t.fontSize            = size;
    t.horizontalOverflow  = horizontal;
    t.verticalOverflow    = vertical;
    t.alignByGeometry     = false;
    t.color               = color;
    t.raycastTarget       = false;
  }

  void Awake() {
    _runner = GetComponentInParent<NetworkRunner>();
    HideAll();
  }

  void HideAll() {
    if (_rootGroup != null) {
      _rootGroup.alpha = 0f;
      _rootGroup.blocksRaycasts = false;
      _rootGroup.interactable = false;
    }
    if (_fillImage != null) _fillImage.fillAmount = 0f;
    if (_spellNameText != null) _spellNameText.text = "";
    _displayFill = 0f;
  }

  void Update() {
    if (_runner == null || !_runner.IsRunning) {
      HideAll();
      return;
    }

    if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj)
        || !playerObj.TryGetComponent(out NetworkCombatController combat)) {
      HideAll();
      return;
    }

    if (combat.CurrentSpellId == 0 || _runner.Tick >= combat.CastEndTick) {
      HideAll();
      return;
    }

    var spell = SpellRegistry.Get(combat.CurrentSpellId);
    if (!spell.IsValid) {
      HideAll();
      return;
    }

    if (_rootGroup == null || _fillImage == null) {
      return;
    }

    _rootGroup.alpha = 1f;

    string nameLabel = spell.Name;
    if (_spellNameText != null) {
      _spellNameText.text = nameLabel;
    }

    float targetFill = Mathf.Clamp01(combat.CastProgress);

    float dt = Time.deltaTime * _fillSmoothSpeed;
    _displayFill = Mathf.Clamp01(Mathf.MoveTowards(_displayFill, targetFill, dt));
    _fillImage.fillAmount = _displayFill;
  }
}
