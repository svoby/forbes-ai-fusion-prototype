using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only sphere visual for the Fireball in-flight missile.
/// Reads already-replicated <see cref="NetworkCombatController"/> properties
/// (<see cref="NetworkCombatController.PendingImpactSpellId"/>,
/// <see cref="NetworkCombatController.PendingImpactTarget"/>,
/// <see cref="NetworkCombatController.PendingMissileReleaseTick"/>) to
/// approximate the missile position each render frame.
/// <para>
/// Authority contract: this component never applies damage, never writes a
/// networked field, has no <see cref="HasStateAuthority"/> guard, and is not a
/// <see cref="NetworkBehaviour"/>. The authoritative missile lives entirely inside
/// <see cref="NetworkCombatController.TryResolvePendingImpact"/>.
/// </para>
/// <para>
/// Known approximation: the lerp origin is the caster's <em>current</em> position,
/// not the position at release tick. If the caster moves after releasing the missile
/// the visual arc drifts slightly from the authoritative homing path; this is
/// acceptable for a cosmetic-only indicator.
/// </para>
/// </summary>
[RequireComponent(typeof(NetworkCombatController))]
public class CosmeticProjectileView : MonoBehaviour {
  const float VisualDiameter = 0.3f;

  NetworkCombatController _ncc;
  GameObject              _sphere;

  void Awake() {
    _ncc = GetComponent<NetworkCombatController>();

    _sphere      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    _sphere.name = "FireballVisual";
    _sphere.transform.localScale = Vector3.one * VisualDiameter;

    // Cosmetic only — must not trigger physics events
    if (_sphere.TryGetComponent<Collider>(out var col)) {
      Destroy(col);
    }

    // renderer.material creates an instance copy of the default material (one sphere, no pooling needed)
    var mat = _sphere.GetComponent<Renderer>().material;
    mat.color = SpellVisualColors.Fireball;
    if (mat.HasProperty("_BaseColor")) {
      mat.SetColor("_BaseColor", SpellVisualColors.Fireball);
    }

    _sphere.SetActive(false);
  }

  void OnDestroy() {
    if (_sphere != null) {
      Destroy(_sphere);
    }
  }

  void LateUpdate() {
    var  runner  = _ncc.Runner;
    byte spellId = _ncc.PendingImpactSpellId;

    if (runner == null || spellId == 0) {
      _sphere.SetActive(false);
      return;
    }

    var spell = SpellRegistry.Get(spellId);
    if (!SpellTravelLogic.HasProjectile(spell)) {
      _sphere.SetActive(false);
      return;
    }

    if (!runner.TryFindObject(_ncc.PendingImpactTarget, out var targetObj) || targetObj == null) {
      _sphere.SetActive(false);
      return;
    }

    Vector3 casterPos = transform.position;
    Vector3 targetPos = targetObj.transform.position;

    float dist    = Vector3.Distance(casterPos, targetPos);
    float elapsed = (runner.Tick - _ncc.PendingMissileReleaseTick) * runner.DeltaTime;
    float t       = dist > 0.001f
      ? Mathf.Clamp01(elapsed * spell.ProjectileSpeedMetersPerSecond / dist)
      : 1f;

    _sphere.transform.position = Vector3.Lerp(casterPos, targetPos, t);
    _sphere.SetActive(true);
  }
}
