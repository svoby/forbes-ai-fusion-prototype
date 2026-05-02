using UnityEngine;

/// <summary>
/// Render-only mirror of <see cref="Health.IsDead"/>: hides the mesh while the
/// owner is dead and shows it again on respawn. No network state of its own.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class HealthView : MonoBehaviour {
  [SerializeField] MeshRenderer _renderer;

  Health _health;

  void Awake() {
    _health = GetComponent<Health>();
    if (_renderer == null) {
      _renderer = GetComponent<MeshRenderer>();
    }
  }

  void OnEnable() {
    if (_health != null) {
      _health.IsDeadChanged += ApplyDeadVisual;
    }
  }

  void OnDisable() {
    if (_health != null) {
      _health.IsDeadChanged -= ApplyDeadVisual;
    }
  }

  void ApplyDeadVisual(bool isDead) {
    if (_renderer != null) {
      _renderer.enabled = !isDead;
    }
  }
}
