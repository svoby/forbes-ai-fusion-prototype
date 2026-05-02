using UnityEngine;

/// <summary>
/// Renders a golden selection ring under the locally selected target.
/// Attach to a dedicated scene GameObject (not on the target itself).
/// <see cref="TargetingController"/> calls <see cref="SetTarget"/> when selection changes.
/// Uses a <see cref="LineRenderer"/> in a circle pattern — no custom shaders required.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TargetHighlight : MonoBehaviour {
  [SerializeField] float _radius   = 0.9f;
  [SerializeField] int   _segments = 32;
  [SerializeField] float _width    = 0.08f;
  [SerializeField] Color _color    = new Color(1f, 0.85f, 0f, 1f);  // gold

  static TargetHighlight _instance;
  public static TargetHighlight Instance => _instance;

  LineRenderer _ring;
  Transform    _target;

  void Awake() {
    _instance = this;
    _ring = GetComponent<LineRenderer>();
    BuildRing();
    gameObject.SetActive(false);
  }

  void OnDestroy() {
    if (_instance == this) {
      _instance = null;
    }
  }

  public void SetTarget(Targetable t) {
    _target = t != null ? t.transform : null;
    gameObject.SetActive(_target != null);
  }

  void LateUpdate() {
    if (_target == null) {
      gameObject.SetActive(false);
      return;
    }

    // Sit just above the floor at the target's feet.
    transform.position = _target.position + Vector3.up * 0.04f;
    transform.rotation = Quaternion.identity;
  }

  void BuildRing() {
    if (_ring == null) {
      return;
    }

    _ring.useWorldSpace  = false;
    _ring.loop           = true;
    _ring.positionCount  = _segments;
    _ring.startWidth     = _width;
    _ring.endWidth       = _width;
    _ring.startColor     = _color;
    _ring.endColor       = _color;

    // Build a flat circle in local XZ space.
    for (int i = 0; i < _segments; i++) {
      float angle = i * Mathf.PI * 2f / _segments;
      _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius));
    }

    // Use the Sprites/Default shader which is available in both Built-in RP and URP.
    // Assign a custom material in the Inspector for better visuals if desired.
    if (_ring.sharedMaterial == null) {
      var mat = new Material(Shader.Find("Sprites/Default"));
      mat.color = _color;
      _ring.sharedMaterial = mat;
    }
  }
}
