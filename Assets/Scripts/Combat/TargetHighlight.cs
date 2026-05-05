using UnityEngine;

/// <summary>
/// Renders a golden selection ring under the locally selected target.
/// Attach to a dedicated scene GameObject (not on the target itself).
/// <see cref="TargetingController"/> calls <see cref="SetTarget"/> when selection changes.
/// When no target is selected the <see cref="LineRenderer"/> is disabled; the GameObject stays active
/// so siblings on <c>[TargetingSystem]</c> (e.g. <see cref="TargetingController"/>, <see cref="SelectedTargetHealthBar"/>) keep receiving Unity events.
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
    // Do not deactivate this GameObject: <c>[TargetingSystem]</c> also hosts
    // <see cref="TargetingController"/> and <see cref="SelectedTargetHealthBar"/>, which must keep running.
    _ring.enabled = false;
  }

  void OnDestroy() {
    if (_instance == this) {
      _instance = null;
    }
  }

  public void SetTarget(Targetable t) {
    _target = t != null ? t.transform : null;
    if (_ring != null) {
      _ring.enabled = _target != null;
    }
  }

  void LateUpdate() {
    if (_target == null) {
      if (_ring != null) {
        _ring.enabled = false;
      }
      return;
    }

    // Resolve the world-space feet position so the ring sits on the ground
    // regardless of whether the pivot is at the character's centre or base.
    Vector3 feet = GetFeetPosition(_target);
    transform.position = feet + Vector3.up * 0.02f;
    transform.rotation = Quaternion.identity;
  }

  /// <summary>
  /// Returns the bottom-of-collider world position for <paramref name="t"/>.
  /// Falls back to <c>t.position</c> when no recognised collider is present.
  /// </summary>
  static Vector3 GetFeetPosition(Transform t) {
    // CharacterController pivot is at the capsule centre; bottom = pos - up*(height/2 - skinWidth).
    var cc = t.GetComponentInChildren<CharacterController>();
    if (cc != null) {
      return cc.transform.position + cc.center - Vector3.up * (cc.height * 0.5f - cc.skinWidth);
    }

    // CapsuleCollider pivot may also be at centre.
    var cap = t.GetComponentInChildren<CapsuleCollider>();
    if (cap != null) {
      Vector3 worldCenter = cap.transform.TransformPoint(cap.center);
      float halfHeight = cap.height * 0.5f * cap.transform.lossyScale.y;
      return worldCenter - Vector3.up * halfHeight;
    }

    return t.position;
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
