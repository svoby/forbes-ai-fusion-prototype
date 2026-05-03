using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local presentation only: world-space fill bar for the <see cref="TargetingController"/> current target when it has live <see cref="Health"/>.
/// </summary>
[DisallowMultipleComponent]
public class SelectedTargetHealthBar : MonoBehaviour {
  [SerializeField] float _verticalOffset = 1.85f;

  [Tooltip("World-space width of the bar (height follows aspect from rect).")]
  [SerializeField] float _barWorldWidth = 0.55f;

  [Tooltip("Rect height / width for the canvas rect before uniform scale.")]
  [SerializeField] float _heightOverWidth = 0.18f;

  TargetingController _targeting;
  Transform           _followRoot;
  Canvas              _canvas;
  Image               _fillImage;

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
      SetBarVisible(false);
      return;
    }

    Vector3 worldPos = target.transform.position + Vector3.up * _verticalOffset;
    _followRoot.position = worldPos;
    _followRoot.rotation = TargetHealthBarLogic.ComputeBillboardRotation(
      worldPos,
      cam.transform.position,
      Quaternion.identity,
      Vector3.up);

    _fillImage.fillAmount = TargetHealthBarLogic.ComputeHealthFill(
      health.NetworkedHealth,
      health.StartingHealth);

    SetBarVisible(true);
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
    bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
    bgImg.raycastTarget = false;

    var fillGo = new GameObject("Fill");
    fillGo.transform.SetParent(canvasGo.transform, false);
    fillGo.layer = canvasGo.layer;
    var fillRt = fillGo.AddComponent<RectTransform>();
    StretchFullRect(fillRt);
    _fillImage = fillGo.AddComponent<Image>();
    _fillImage.type = Image.Type.Filled;
    _fillImage.fillMethod = Image.FillMethod.Horizontal;
    _fillImage.fillOrigin = 0;
    _fillImage.color = new Color(0.25f, 0.78f, 0.35f, 1f);
    _fillImage.raycastTarget = false;
    _fillImage.fillAmount = 1f;
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

  internal float CurrentFill01 => _fillImage != null ? _fillImage.fillAmount : 0f;

  internal bool IsBarVisible => _canvas != null && _canvas.enabled;
}
