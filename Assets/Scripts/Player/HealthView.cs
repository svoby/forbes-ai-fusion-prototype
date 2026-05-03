using UnityEngine;

/// <summary>
/// Render-only mirror of <see cref="Health.IsDead"/>: disables every
/// <see cref="Renderer"/> on this object and its descendants while dead, then
/// restores them on respawn. Prefab-placed geometry (e.g. facing markers) needs
/// no extra wiring.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class HealthView : MonoBehaviour {
  Renderer[] _renderers;

  void Awake() {
    _renderers = GetComponentsInChildren<Renderer>(true);
  }

  void OnEnable() {
    var health = GetComponent<Health>();
    if (health != null) {
      health.IsDeadChanged += ApplyDeadVisual;
    }
  }

  void OnDisable() {
    var health = GetComponent<Health>();
    if (health != null) {
      health.IsDeadChanged -= ApplyDeadVisual;
    }
  }

  void ApplyDeadVisual(bool isDead) {
    if (_renderers == null) {
      return;
    }
    var alive = !isDead;
    for (var i = 0; i < _renderers.Length; i++) {
      var r = _renderers[i];
      if (r != null) {
        r.enabled = alive;
      }
    }
  }
}
