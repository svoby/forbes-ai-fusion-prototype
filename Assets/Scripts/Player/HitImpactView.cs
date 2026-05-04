using UnityEngine;

/// <summary>
/// Cosmetic-only hit feedback: subscribes to <see cref="Health.CombatHitReceived"/>
/// and spawns a brief local-only flash at the target's center mass whenever damage lands.
/// <para>
/// Authority contract: this component never applies damage, never writes a networked
/// field, and spawns no <see cref="Fusion.NetworkObject"/>. The visual is local-only
/// (each client renders it independently) and has no effect on any game state.
/// See <c>docs/COMBAT_FEEDBACK_POLICY.md</c> for the full pipeline.
/// </para>
/// <para>
/// The flash is intentionally minimal (primitive sphere, no pooling) so it is easy
/// to replace later with a real VFX prefab or particle system: swap the body of
/// <see cref="SpawnHitFlash"/> and keep the rest unchanged.
/// </para>
/// </summary>
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class HitImpactView : MonoBehaviour {
  const float FlashDuration    = 0.2f;
  const float FlashDiameter    = 0.65f;
  const float CenterMassOffset = 1.0f;

  static readonly Color HitColor = new Color(1f, 0.08f, 0.08f);

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
    SpawnHitFlash();
  }

  void SpawnHitFlash() {
    var sphere  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.name = "HitImpactFlash";
    sphere.transform.position   = ComputeCenterMass(transform);
    sphere.transform.localScale = Vector3.one * FlashDiameter;

    // Collider rule (COMBAT_FEEDBACK_POLICY.md §collider-rule): disable synchronously
    // before physics runs — Destroy() is deferred and leaves the collider live for one
    // FixedUpdate, which can deflect nearby CharacterControllers.
    if (sphere.TryGetComponent<Collider>(out var col)) {
      col.enabled = false;
      Destroy(col);
    }

    sphere.GetComponent<Renderer>().material = SpellVisualColors.NewUnlitOrbMaterial(HitColor);
    Destroy(sphere, FlashDuration);
  }

  static Vector3 ComputeCenterMass(Transform target) {
    if (target.TryGetComponent<Collider>(out var col) && col.enabled) {
      return col.bounds.center;
    }
    return target.position + Vector3.up * CenterMassOffset;
  }
}
