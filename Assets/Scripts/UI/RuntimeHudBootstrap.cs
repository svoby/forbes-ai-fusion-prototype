using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the runtime HUD canvas under a <see cref="NetworkRunner"/> and wires the
/// player-facing HUD views that render on that canvas.
/// </summary>
[DisallowMultipleComponent]
public class RuntimeHudBootstrap : MonoBehaviour {
  public const string HudCanvasChildName = "ForbesHudCanvas";

  /// <summary>Bump when default HUD geometry changes so stale runtime UI can be rebuilt.</summary>
  public const int CurrentHudLayoutVersion = 6;

  [SerializeField] int _builtLayoutVersion;

  public bool UsesCurrentHudLayout => _builtLayoutVersion >= CurrentHudLayoutVersion
                                      && GetComponent<Canvas>() != null
                                      && GetComponentInChildren<CastBarView>(true) != null
                                      && GetComponentInChildren<CombatFeedbackBannerView>(true) != null;

  public void StampLayoutVersion(int version) {
    _builtLayoutVersion = version;
  }

  /// <summary>
  /// Ensures one current runtime HUD exists under <paramref name="runner"/>.
  /// Call after a one-frame delay when replacing an existing HUD so queued
  /// <see cref="Object.Destroy"/> calls have flushed before rebuilding.
  /// </summary>
  public static RuntimeHudBootstrap EnsureForRunner(NetworkRunner runner) {
    if (runner == null) {
      return null;
    }

    foreach (var hud in runner.GetComponentsInChildren<RuntimeHudBootstrap>(true)) {
      if (hud != null && hud.UsesCurrentHudLayout) {
        return hud;
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

  static RuntimeHudBootstrap CreateHudUnderRunner(NetworkRunner runner) {
    Font uiFont = CastBarView.ResolveDefaultHudFont(26);

    Sprite white = Sprite.Create(
      Texture2D.whiteTexture,
      new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
      new Vector2(0.5f, 0.5f),
      100f);

    GameObject canvasGo = new GameObject(HudCanvasChildName);
    canvasGo.transform.SetParent(runner.transform, false);
    ConfigureCanvas(canvasGo);

    var hud = canvasGo.AddComponent<RuntimeHudBootstrap>();
    hud.StampLayoutVersion(CurrentHudLayoutVersion);

    BuildCastBarPanel(canvasGo, uiFont, white);
    BuildBannerPanel(canvasGo, uiFont);
    return hud;
  }

  static void ConfigureCanvas(GameObject canvasGo) {
    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 1000;

    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode           = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution   = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight    = 0.5f;
    scaler.referencePixelsPerUnit = 100f;
    canvasGo.AddComponent<GraphicRaycaster>();
  }

  public static CastBarView BuildCastBarPanel(GameObject canvasGo, Font uiFont, Sprite white) {
    GameObject panel = new GameObject("CastBarPanel");
    panel.transform.SetParent(canvasGo.transform, false);
    var panelRect = panel.AddComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0.5f, 0f);
    panelRect.anchorMax = new Vector2(0.5f, 0f);
    panelRect.pivot = new Vector2(0.5f, 0f);
    panelRect.anchoredPosition = new Vector2(0f, CastBarView.CastBarLiftFromBottomPx);
    panelRect.sizeDelta = new Vector2(CastBarView.CastBarPanelWidth, CastBarView.CastBarPanelHeight);
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
    CastBarView.StyleHudText(nameTxt, uiFont, 26, FontStyle.Bold, Color.white);
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
    return view;
  }

  /// <summary>
  /// Builds the WoW-style centered combat feedback banner panel under <paramref name="canvasGo"/>.
  /// Position: horizontally full-width, vertically centered at 1/phi^2, about 38 percent from top.
  /// </summary>
  public static CombatFeedbackBannerView BuildBannerPanel(GameObject canvasGo, Font uiFont) {
    const float phi      = 1.618033988749f;
    const float yFromTop = 1080f / (phi * phi);

    var bannerGo = new GameObject("BannerPanel");
    bannerGo.transform.SetParent(canvasGo.transform, false);

    var bannerRect = bannerGo.AddComponent<RectTransform>();
    bannerRect.anchorMin        = new Vector2(0f, 1f);
    bannerRect.anchorMax        = new Vector2(1f, 1f);
    bannerRect.pivot            = new Vector2(0.5f, 0.5f);
    bannerRect.anchoredPosition = new Vector2(0f, -yFromTop);
    bannerRect.sizeDelta        = new Vector2(0f, 60f);

    var bannerGroup = bannerGo.AddComponent<CanvasGroup>();
    bannerGroup.alpha          = 0f;
    bannerGroup.blocksRaycasts = false;
    bannerGroup.interactable   = false;

    var labelGo = new GameObject("Label");
    labelGo.transform.SetParent(bannerGo.transform, false);
    StretchFull(labelGo.AddComponent<RectTransform>());

    var labelText = labelGo.AddComponent<Text>();
    CastBarView.StyleHudText(labelText, uiFont, 20, FontStyle.Bold, new Color(1f, 0.25f, 0.2f));
    labelText.alignment = TextAnchor.MiddleCenter;
    labelText.text      = "";

    var shadow = labelGo.AddComponent<Shadow>();
    shadow.effectColor    = new Color(0f, 0f, 0f, 0.82f);
    shadow.effectDistance = new Vector2(2f, -2f);

    var bannerView = canvasGo.AddComponent<CombatFeedbackBannerView>();
    bannerView.BindUi(bannerGroup, labelText);
    return bannerView;
  }

  static void StretchFull(RectTransform rt) {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    rt.localScale = Vector3.one;
  }
}
