using UnityEngine;

/// <summary>
/// Cosmetic-only hit feedback: subscribes to <see cref="Health.CombatHitReceived"/>
/// and displays a floating screen-space damage number above the target whenever
/// damage lands.
/// <para>
/// Authority contract: this component never applies damage, never writes a networked
/// field, and spawns no <see cref="Fusion.NetworkObject"/>. The visual is local-only
/// (each client renders it independently) and has no effect on any game state.
/// See <c>docs/COMBAT_FEEDBACK_POLICY.md</c> for the full pipeline.
/// </para>
/// <para>
/// The damage number is rendered in screen space via
/// <see cref="FloatingCombatTextCanvas.ShowDamage"/>, so it stays readable at any
/// camera distance without billboard rotation or world-space sizing.
/// To replace the visual: call a different UI service inside <see cref="OnHit"/>.
/// </para>
/// </summary>
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class HitImpactView : MonoBehaviour {
  Health _health;

  void Awake() {
    _health = GetComponent<Health>();
  }

  void OnEnable() {
    if (_health != null) {
      _health.CombatHitReceived += OnHit;
    }
  }

  void OnDisable() {
    if (_health != null) {
      _health.CombatHitReceived -= OnHit;
    }
  }

  void OnHit(float damage) {
    FloatingCombatTextCanvas.ShowDamage(transform, damage);
  }
}
