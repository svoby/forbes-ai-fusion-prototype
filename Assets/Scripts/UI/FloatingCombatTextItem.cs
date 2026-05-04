using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One floating damage number in screen space.
/// Spawned and parented by <see cref="FloatingCombatTextCanvas"/>; self-destructs
/// after <see cref="Init"/> lifetime expires.
/// <para>
/// Cosmetic only: no gameplay state, no networked fields, no colliders.
/// The world anchor is re-projected every <c>LateUpdate</c> so the text follows
/// the target while animating upward in screen pixels.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class FloatingCombatTextItem : MonoBehaviour {
  const float MaxPixelRise = 80f;

  RectTransform _rt;
  Text          _label;
  CanvasGroup   _group;

  Transform _worldAnchorTarget;
  float     _lifetime;
  float     _elapsed;
  Vector3   _lastValidScreenPos;

  /// <summary>
  /// Wires the item. Called immediately after the component is added to the
  /// canvas child GameObject.
  /// </summary>
  public void Init(Transform worldAnchor, string text, float lifetime) {
    _worldAnchorTarget = worldAnchor;
    _lifetime          = lifetime;
    _elapsed           = 0f;

    _rt    = GetComponent<RectTransform>();
    _label = GetComponent<Text>();
    _group = GetComponent<CanvasGroup>();

    if (_label != null) {
      _label.text = text;
    }

    // Pin the initial screen position so the item appears in the right place
    // immediately (before the first LateUpdate tick).
    _lastValidScreenPos = SampleScreenPos();
    if (_rt != null) {
      _rt.position = _lastValidScreenPos;
    }
  }

  void LateUpdate() {
    _elapsed += Time.deltaTime;

    if (_elapsed >= _lifetime) {
      Destroy(gameObject);
      return;
    }

    var screenPos = SampleScreenPos();

    // Only accept the new position when the target is in front of the camera.
    if (!FloatingCombatTextLogic.IsBehindCamera(screenPos)) {
      _lastValidScreenPos = screenPos;
    }

    float pixelOffset = FloatingCombatTextLogic.ComputePixelOffset(_elapsed, _lifetime, MaxPixelRise);
    float alpha       = FloatingCombatTextLogic.ComputeAlpha(_elapsed, _lifetime);

    if (_rt != null) {
      var pos = _lastValidScreenPos;
      pos.y       += pixelOffset;
      _rt.position = pos;
    }

    if (_group != null) {
      _group.alpha = alpha;
    }
  }

  // ── private helpers ──────────────────────────────────────────────────────────

  Vector3 SampleScreenPos() {
    var cam = Camera.main;
    if (cam == null || _worldAnchorTarget == null) {
      return _lastValidScreenPos;
    }

    return cam.WorldToScreenPoint(FloatingCombatTextLogic.GetWorldAnchor(_worldAnchorTarget));
  }
}
