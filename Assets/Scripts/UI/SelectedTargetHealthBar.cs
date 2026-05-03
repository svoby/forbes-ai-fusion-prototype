using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local presentation only: world-space fill bar for the <see cref="TargetingController"/> current target when it has live <see cref="Health"/>.
/// </summary>
[DisallowMultipleComponent]
public class SelectedTargetHealthBar : MonoBehaviour {
  static Sprite _whiteUiSprite;

  [SerializeField] float _verticalOffset = 1.85f;

  [Tooltip("World-space width of the bar (height follows aspect from rect).")]
  [SerializeField] float _barWorldWidth = 0.55f;

  [Tooltip("Rect height / width for the canvas rect before uniform scale.")]
  [SerializeField] float _heightOverWidth = 0.18f;

  TargetingController _targeting;
  Transform           _followRoot;
  Canvas              _canvas;
  Image               _fillImage;
  RectTransform       _fillRect;

  Health _wiredHealth;

  float _displayedFill01 = 1f;

  void Awake() {
    _targeting = GetComponent<TargetingController>();
    BuildWorldBar();
    SetBarVisible(false);
  }

  void LateUpdate() {
    if (_targeting == null) {
      _targeting = GetComponent<TargetingController>();
    }

    Targetable target = _targeting != null ? _targeting.CurrentTarget : null;
    var cam = Camera.main;
    if (target == null ||
        cam == null ||
        !target.TryGetComponent(out Health health) ||
        health.IsDead ||
        health.StartingHealth <= 0f) {
      UnwireHealth();
      SetBarVisible(false);
      return;
    }

    TryWireHealth(health);

    Vector3 worldPos = target.transform.position + Vector3.up * _verticalOffset;
    _followRoot.position = worldPos;
    _followRoot.rotation = TargetHealthBarLogic.ComputeBillboardRotation(
      worldPos,
      cam.transform.position,
      Quaternion.identity,
      Vector3.up);

    ApplyHpFill(health.NetworkedHealth, health.StartingHealth);

    SetBarVisible(true);
  }

  void OnDisable() {
    UnwireHealth();
  }

  void OnDestroy() {
    UnwireHealth();
  }

  void TryWireHealth(Health health) {
    if (_wiredHealth == health) {
      return;
    }

    UnwireHealth();
    _wiredHealth = health;
    _wiredHealth.NetworkedHealthRenderChanged += OnWiredHealthHpRender;
    OnWiredHealthHpRender(_wiredHealth.NetworkedHealth);
  }

  void UnwireHealth() {
    if (_wiredHealth != null) {
      _wiredHealth.NetworkedHealthRenderChanged -= OnWiredHealthHpRender;
      _wiredHealth = null;
    }
  }

  void OnWiredHealthHpRender(float hp) {
    if (_wiredHealth == null) {
      return;
    }

    ApplyHpFill(hp, _wiredHealth.StartingHealth);
  }

  /// <summary>
  /// Uses <see cref="RectTransform"/> horizontal anchors instead of <see cref="Image.Type.Filled"/>,
  /// so HP ratio is visibly obvious even when Filled/UI backend misbehaves in world-space canvases.
  /// </summary>
  void ApplyHpFill(float currentHp, float maxHp) {
    if (_fillRect == null) {
      return;
    }

    _displayedFill01 = TargetHealthBarLogic.ApplyHorizontalHpAnchors(_fillRect, currentHp, maxHp);

    // Keep Fill simple (full tinted quad inside the anchored strip); avoids Filled quirks.
    if (_fillImage != null) {
      _fillImage.fillAmount = 1f;
    }
  }

  void BuildWorldBar() {
    _followRoot = new GameObject(nameof(SelectedTargetHealthBar) + "_Follow").transform;
    _followRoot.SetParent(transform, false);
    _followRoot.localPosition = Vector3.zero;
    _followRoot.localRotation = Quaternion.identity;
    _followRoot.localScale = Vector3.one;

    var canvasGo = new GameObject("Canvas");
    canvasGo.transform.SetParent(_followRoot, false);
    canvasGo.layer = gameObject.layer;

    _canvas = canvasGo.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.WorldSpace;

    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.dynamicPixelsPerUnit = 100f;

    var canvasRt = canvasGo.GetComponent<RectTransform>();
    float w = 100f;
    float h = Mathf.Max(8f, w * _heightOverWidth);
    canvasRt.sizeDelta = new Vector2(w, h);
    float scale = _barWorldWidth / w;
    canvasGo.transform.localScale = new Vector3(scale, scale, scale);
    canvasGo.transform.localPosition = Vector3.zero;
    canvasGo.transform.localRotation = Quaternion.identity;

    var bgGo = new GameObject("Background");
    bgGo.transform.SetParent(canvasGo.transform, false);
    bgGo.layer = canvasGo.layer;
    var bgRt = bgGo.AddComponent<RectTransform>();
    StretchFullRect(bgRt);
    var bgImg = bgGo.AddComponent<Image>();
    UiApplyWhiteSprite(bgImg);
    bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
    bgImg.raycastTarget = false;

    var fillGo = new GameObject("Fill");
    fillGo.transform.SetParent(canvasGo.transform, false);
    fillGo.layer = canvasGo.layer;
    _fillRect = fillGo.AddComponent<RectTransform>();
    StretchFullRectAnchorLeft(_fillRect);
    _fillImage = fillGo.AddComponent<Image>();
    UiApplyWhiteSprite(_fillImage);
    _fillImage.type = Image.Type.Simple;
    _fillImage.color = new Color(0.25f, 0.78f, 0.35f, 1f);
    _fillImage.raycastTarget = false;
    _fillImage.preserveAspect = false;
    _fillImage.fillAmount = 1f;
  }

  /// <summary>Full-width strip; horizontal extent is driven by <see cref="ApplyHpFill"/> via <c>anchorMax.x</c>.</summary>
  static void StretchFullRectAnchorLeft(RectTransform rt) {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    rt.localScale = Vector3.one;
    rt.localPosition = Vector3.zero;
  }

  /// <summary>
  /// Unity UI <see cref="Image.Type.Filled"/> needs a sprite; without it, changing
  /// <see cref="Image.fillAmount"/> often has no visible effect despite the value updating.
  /// </summary>
  static void UiApplyWhiteSprite(Image img) {
    if (img == null || img.sprite != null) {
      return;
    }

    if (_whiteUiSprite == null) {
      Texture2D tex = Texture2D.whiteTexture;
      _whiteUiSprite = Sprite.Create(
        tex,
        new Rect(0f, 0f, tex.width, tex.height),
        new Vector2(0.5f, 0.5f),
        100f);
      _whiteUiSprite.hideFlags = HideFlags.DontSave;
    }

    img.sprite = _whiteUiSprite;
  }

  static void StretchFullRect(RectTransform rt) {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.localScale = Vector3.one;
    rt.localPosition = Vector3.zero;
  }

  void SetBarVisible(bool visible) {
    if (_canvas != null) {
      _canvas.enabled = visible;
    }
  }

  internal float CurrentFill01 => _displayedFill01;

  internal bool IsBarVisible => _canvas != null && _canvas.enabled;
}
