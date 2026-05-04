using UnityEngine;

/// <summary>
/// Cosmetic-only hit feedback: subscribes to <see cref="Health.CombatHitReceived"/>
/// and spawns a floating damage number above the target whenever damage lands.
/// <para>
/// Authority contract: this component never applies damage, never writes a networked
/// field, and spawns no <see cref="Fusion.NetworkObject"/>. The visual is local-only
/// (each client renders it independently) and has no effect on any game state.
/// See <c>docs/COMBAT_FEEDBACK_POLICY.md</c> for the full pipeline.
/// </para>
/// <para>
/// To replace the visual: swap the body of <see cref="SpawnDamageText"/> and keep
/// the subscription wiring unchanged.
/// </para>
/// </summary>
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class HitImpactView : MonoBehaviour {
  const float TextLifetimeSec = 0.9f;
  const float AboveHeadOffset = 0.25f; // metres above top of collider bounds

  static readonly Color HitColor = new Color(1f, 0.15f, 0.15f);

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
    SpawnDamageText(damage);
  }

  void SpawnDamageText(float damage) {
    var go       = new GameObject("HitDamageText");
    go.transform.position = ComputeAboveHead(transform);

    var tm       = go.AddComponent<TextMesh>();
    tm.text      = Mathf.RoundToInt(damage).ToString();
    tm.fontSize  = 60;
    tm.color     = HitColor;
    tm.alignment = TextAlignment.Center;
    tm.anchor    = TextAnchor.MiddleCenter;

    var floater  = go.AddComponent<DamageFloatText>();
    floater.Init(TextLifetimeSec);
  }

  static Vector3 ComputeAboveHead(Transform target) {
    if (target.TryGetComponent<Collider>(out var col) && col.enabled) {
      var b = col.bounds;
      return new Vector3(b.center.x, b.max.y + AboveHeadOffset, b.center.z);
    }
    return target.position + Vector3.up * 2.3f;
  }
}

/// <summary>
/// Internal helper spawned by <see cref="HitImpactView"/>: floats the damage text
/// upward and keeps it facing the camera (billboard). Self-destructs after lifetime.
/// Cosmetic only — no gameplay state, no networked fields.
/// </summary>
class DamageFloatText : MonoBehaviour {
  const float FloatSpeed = 1.2f; // metres per second upward

  float _lifetime;
  float _elapsed;

  internal void Init(float lifetime) {
    _lifetime = lifetime;
  }

  void LateUpdate() {
    _elapsed += Time.deltaTime;

    if (_elapsed >= _lifetime) {
      Destroy(gameObject);
      return;
    }

    transform.position += Vector3.up * (FloatSpeed * Time.deltaTime);

    var cam = Camera.main;
    if (cam != null) {
      transform.forward = cam.transform.forward;
    }
  }
}
