using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only spell impact flash — plays a short-lived sphere burst at the
/// target's center mass when a spell successfully deals damage.
/// <para>
/// Called from <see cref="NetworkCombatController.RpcOnSpellImpact"/> which is
/// dispatched by State Authority to all clients immediately after
/// <see cref="Health.DealDamageRpc"/> is called. This component never applies
/// damage, never writes a networked field, and is not a
/// <see cref="NetworkBehaviour"/>.
/// </para>
/// <para>
/// The flash is intentionally minimal (primitive sphere, no pooling) so it is
/// easy to replace later with a real VFX prefab or particle system: swap the
/// body of <see cref="SpawnFlash"/> and keep the rest unchanged.
/// </para>
/// </summary>
[RequireComponent(typeof(NetworkCombatController))]
public class SpellImpactView : MonoBehaviour {
  const float FlashDuration  = 0.35f;
  const float FlashDiameter  = 0.8f;
  const float CenterMassOffset = 1.0f;

  NetworkCombatController _ncc;

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

  // ── Helpers ──────────────────────────────────────────────────────────────────

  static Vector3 ComputeCenterMass(Transform target) {
    if (target.TryGetComponent<Collider>(out var col) && col.enabled) {
      return col.bounds.center;
    }
    return target.position + Vector3.up * CenterMassOffset;
  }

  static void SpawnFlash(Vector3 worldPos) {
    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.name = "SpellImpactFlash";
    sphere.transform.position   = worldPos;
    sphere.transform.localScale = Vector3.one * FlashDiameter;

    if (sphere.TryGetComponent<Collider>(out var col)) {
      // Disable immediately (synchronous) so the collider never participates in
      // physics — Destroy() is deferred and would leave it live for one FixedUpdate,
      // deflecting nearby CharacterControllers.
      col.enabled = false;
      Destroy(col);
    }

    sphere.GetComponent<Renderer>().material = SpellVisualColors.NewFireballOrbMaterial();

    Destroy(sphere, FlashDuration);
  }
}
