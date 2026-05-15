using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only spell impact flash: plays a short-lived prefab burst at the
/// target's center mass when a spell successfully deals damage.
/// <para>
/// Called from <see cref="NetworkCombatController.RpcOnSpellImpact"/> which is
/// dispatched by State Authority to all clients immediately after
/// <see cref="Health.DealDamageRpc"/> is called. This component never applies
/// damage, never writes a networked field, and is not a
/// <see cref="NetworkBehaviour"/>.
/// </para>
/// <para>
/// The flash is intentionally minimal and prefab-driven so it can be replaced
/// later with a real VFX prefab or particle system without changing gameplay.
/// </para>
/// </summary>
[RequireComponent(typeof(NetworkCombatController))]
public class SpellImpactView : MonoBehaviour {
  const float FlashDuration    = 0.35f;
  const float CenterMassOffset = 1.0f;

  [SerializeField] GameObject _impactPrefab;

  NetworkCombatController _ncc;
  bool                    _missingImpactPrefabWarningLogged;

  void Awake() {
    _ncc = GetComponent<NetworkCombatController>();
  }

  /// <summary>
  /// Called on every client by <see cref="NetworkCombatController.RpcOnSpellImpact"/>.
  /// Resolves the target object and spawns a short-lived cosmetic flash at center mass.
  /// Fails silently if the target cannot be found on this client.
  /// </summary>
  public void OnSpellImpact(byte spellId, NetworkId targetId) {
    var runner = _ncc != null ? _ncc.Runner : null;
    if (runner == null) {
      return;
    }

    if (!runner.TryFindObject(targetId, out var targetObj) || targetObj == null) {
      return;
    }

    Vector3 impactPos = ComputeCenterMass(targetObj.transform);
    SpawnFlash(impactPos);
  }

  static Vector3 ComputeCenterMass(Transform target) {
    if (target.TryGetComponent<Collider>(out var col) && col.enabled) {
      return col.bounds.center;
    }
    return target.position + Vector3.up * CenterMassOffset;
  }

  void SpawnFlash(Vector3 worldPos) {
    if (_impactPrefab == null) {
      if (!_missingImpactPrefabWarningLogged) {
        _missingImpactPrefabWarningLogged = true;
        Debug.LogWarning("[SpellImpactView] Impact prefab missing; spell impact visual skipped.", this);
      }
      return;
    }

    var instance = Instantiate(_impactPrefab, worldPos, Quaternion.identity);
    instance.name = "SpellImpactFlash";
    ActiveSpellInstancePresenter.DisableAndDestroyCollidersInChildren(instance);
    Destroy(instance, FlashDuration);
  }
}
