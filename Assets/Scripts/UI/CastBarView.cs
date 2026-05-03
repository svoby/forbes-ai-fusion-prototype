using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WoW-like cast bar for the local player's <see cref="NetworkCombatController"/>.
/// Presentation-only: reads networked cast ticks and hides for instant casts.
/// <para>
/// At runtime, <see cref="EnsureForRunner"/> builds the canvas under the
/// <see cref="NetworkRunner"/> if none exists (so play mode works without re-running
/// the editor scene setup menu).
/// </para>
/// </summary>
public class CastBarView : MonoBehaviour {
  public const string HudCanvasChildName = "ForbesHudCanvas";

  /// <summary>Bump when default HUD geometry changes so <see cref="EnsureForRunner"/> can rebuild stale UI.</summary>
  public const int CurrentHudLayoutVersion = 5;

  public const float CastBarPanelWidth   = 640f;
  public const float CastBarPanelHeight = 112f;

  /// <summary>Pixels above screen bottom for bottom-anchored cast bar pivot (WoW-like: above thumb / action-bar zone).</summary>
  public const float CastBarLiftFromBottomPx = 172f;

  [SerializeField] int _builtLayoutVersion;

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

  /// <summary>Ready to use built-in HUD geometry (used to skip nuking/recreating unnecessarily).</summary>
  public bool UsesCurrentHudLayout => IsUiBound && _builtLayoutVersion >= CurrentHudLayoutVersion;

  /// <summary>Wires UI built in code or from the editor setup tool.</summary>
  public void BindUi(CanvasGroup rootGroup, Image fillImage, Text spellNameText) {
    _rootGroup      = rootGroup;
    _fillImage      = fillImage;
    _spellNameText  = spellNameText;
  }

  public void StampLayoutVersion(int version) {
    _builtLayoutVersion = version;
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
    Debug.LogWarning(
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

  /// <summary>
  /// Ensures a cast bar exists under <paramref name="runner"/>; creates UI if missing.
  /// Call from the next frame (e.g. after <c>yield return null</c>) if replacing an existing HUD,
  /// so Unity can finish queued <see cref="Object.Destroy"/> calls first.
  /// </summary>
  public static CastBarView EnsureForRunner(NetworkRunner runner) {
    if (runner == null) {
      return null;
    }

    foreach (var v in runner.GetComponentsInChildren<CastBarView>(true)) {
      if (v != null && v.UsesCurrentHudLayout) {
        return v;
      }
    }

    DestroyAllHudCanvasesUnderRunner(runner);
    return CreateHudUnderRunner(runner);
  }

  /// <summary>Removes all direct child canvases matching <see cref="HudCanvasChildName"/>.</summary>
  public static void DestroyAllHudCanvasesUnderRunner(NetworkRunner runner) {
    if (runner == null) {
      return;
    }

    Transform t = runner.transform;
    for (int i = t.childCount - 1; i >= 0; --i) {
      Transform c = t.GetChild(i);
      if (c != null && c.name == HudCanvasChildName) {
        Object.Destroy(c.gameObject);
      }
    }
  }

  static CastBarView CreateHudUnderRunner(NetworkRunner runner) {
    Font uiFont = ResolveDefaultHudFont(26);

    Sprite white = Sprite.Create(
      Texture2D.whiteTexture,
      new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
      new Vector2(0.5f, 0.5f),
      100f);

    GameObject canvasGo = new GameObject(HudCanvasChildName);
    canvasGo.transform.SetParent(runner.transform, false);

    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 1000;

    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode           = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution   = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight    = 0.5f;
    scaler.referencePixelsPerUnit = 100f;
    canvasGo.AddComponent<GraphicRaycaster>();

    GameObject panel = new GameObject("CastBarPanel");
    panel.transform.SetParent(canvasGo.transform, false);
    var panelRect = panel.AddComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0.5f, 0f);
    panelRect.anchorMax = new Vector2(0.5f, 0f);
    panelRect.pivot = new Vector2(0.5f, 0f);
    panelRect.anchoredPosition = new Vector2(0f, CastBarLiftFromBottomPx);
    panelRect.sizeDelta = new Vector2(CastBarPanelWidth, CastBarPanelHeight);
    var panelGroup = panel.AddComponent<CanvasGroup>();
    panelGroup.alpha = 0f;
    panelGroup.blocksRaycasts = false;

    var bg = new GameObject("Background");
    bg.transform.SetParent(panel.transform, false);
    StretchFull(bg.AddComponent<RectTransform>());
    var bgImg = bg.AddComponent<Image>();
    bgImg.sprite = white;
    bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

    var nameGo = new GameObject("SpellName");
    nameGo.transform.SetParent(panel.transform, false);
    var nameRt = nameGo.AddComponent<RectTransform>();
    nameRt.anchorMin = new Vector2(0f, 1f);
    nameRt.anchorMax = new Vector2(1f, 1f);
    nameRt.pivot = new Vector2(0.5f, 1f);
    nameRt.anchoredPosition = new Vector2(0f, -6f);
    nameRt.sizeDelta = new Vector2(-24f, 40f);
    var nameTxt = nameGo.AddComponent<Text>();
    StyleHudText(nameTxt, uiFont, 26, FontStyle.Bold, Color.white);
    nameTxt.alignment = TextAnchor.MiddleCenter;
    nameTxt.text      = "";

    var trackGo = new GameObject("FillTrack");
    trackGo.transform.SetParent(panel.transform, false);
    var trackRt = trackGo.AddComponent<RectTransform>();
    trackRt.anchorMin = new Vector2(0f, 0f);
    trackRt.anchorMax = new Vector2(1f, 0f);
    trackRt.pivot = new Vector2(0.5f, 0f);
    trackRt.anchoredPosition = new Vector2(0f, 20f);
    trackRt.sizeDelta = new Vector2(-24f, 30f);
    var trackImg = trackGo.AddComponent<Image>();
    trackImg.sprite = white;
    trackImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);

    var fillGo = new GameObject("Fill");
    fillGo.transform.SetParent(trackGo.transform, false);
    StretchFull(fillGo.AddComponent<RectTransform>());
    var fillImg = fillGo.AddComponent<Image>();
    fillImg.sprite = white;
    fillImg.type = Image.Type.Filled;
    fillImg.fillMethod = Image.FillMethod.Horizontal;
    fillImg.fillOrigin = 0;
    fillImg.color = new Color(0.85f, 0.45f, 0.12f, 1f);
    fillImg.fillAmount = 0f;

    var view = canvasGo.AddComponent<CastBarView>();
    view.BindUi(panelGroup, fillImg, nameTxt);
    view.StampLayoutVersion(CurrentHudLayoutVersion);
    return view;
  }

  static void StretchFull(RectTransform rt) {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    rt.localScale = Vector3.one;
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
