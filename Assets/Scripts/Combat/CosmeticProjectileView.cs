using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only sphere visual for the Fireball in-flight missile.
/// Reads already-replicated <see cref="PlayerMissileSlot"/> properties
/// (<see cref="PlayerMissileSlot.PendingImpactSpellId"/>,
/// <see cref="PlayerMissileSlot.PendingImpactTarget"/>,
/// <see cref="PlayerMissileSlot.MissileOrigin"/>,
/// <see cref="PlayerMissileSlot.PendingMissileReleaseTick"/>) to
/// approximate the missile position each render frame.
/// <para>
/// Authority contract: this component never applies damage, never writes a
/// networked field, has no <see cref="HasStateAuthority"/> guard, and is not a
/// <see cref="NetworkBehaviour"/>. The authoritative missile lives entirely inside
/// <see cref="PlayerMissileSlot.FixedUpdateNetwork"/>.
/// </para>
/// <para>
/// Visual fidelity: the lerp origin is <see cref="PlayerMissileSlot.MissileOrigin"/>
/// — the replicated caster position at release time. This eliminates the drift that
/// occurred when the caster moved after releasing the missile.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerMissileSlot))]
public class CosmeticProjectileView : MonoBehaviour {
  const float VisualDiameter = 0.3f;

  PlayerMissileSlot _missileSlot;
  GameObject        _sphere;

  void Awake() {
    _missileSlot = GetComponent<PlayerMissileSlot>();

    _sphere      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    _sphere.name = "FireballVisual";
    _sphere.transform.localScale = Vector3.one * VisualDiameter;

    // Cosmetic only — must not trigger physics events.
    // Disable immediately (synchronous) before Destroy's deferred removal.
    if (_sphere.TryGetComponent<Collider>(out var col)) {
      col.enabled = false;
      Destroy(col);
    }

    var rend = _sphere.GetComponent<Renderer>();
    rend.material = SpellVisualColors.NewFireballOrbMaterial();

    _sphere.SetActive(false);
  }

  void OnDestroy() {
    if (_sphere != null) {
      Destroy(_sphere);
    }
  }

  void LateUpdate() {
    var  runner  = _missileSlot.Runner;
    byte spellId = _missileSlot.PendingImpactSpellId;

    if (runner == null || spellId == 0) {
      _sphere.SetActive(false);
      return;
    }

    var spell = SpellRegistry.Get(spellId);
    if (!SpellTravelLogic.HasProjectile(spell)) {
      _sphere.SetActive(false);
      return;
    }

    if (!runner.TryFindObject(_missileSlot.PendingImpactTarget, out var targetObj) || targetObj == null) {
      _sphere.SetActive(false);
      return;
    }

    // Use the replicated release position as the lerp origin so the visual arc
    // stays correct even when the caster moves after firing.
    Vector3 originPos = _missileSlot.MissileOrigin;
    Vector3 targetPos = targetObj.transform.position;

    float dist    = Vector3.Distance(originPos, targetPos);
    float elapsed = (runner.Tick - _missileSlot.PendingMissileReleaseTick) * runner.DeltaTime;
    float t       = dist > 0.001f
      ? Mathf.Clamp01(elapsed * spell.ProjectileSpeedMetersPerSecond / dist)
      : 1f;

    _sphere.transform.position = Vector3.Lerp(originPos, targetPos, t);
    _sphere.SetActive(true);
  }
}
