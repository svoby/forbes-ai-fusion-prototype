using Fusion;
using UnityEngine;

/// <summary>
/// Shared, test-friendly targeting helpers used by <see cref="TargetingController"/>.
/// Keeps filtering and ray-hit resolution in pure static form so regressions surface in EditMode/PlayMode.
/// </summary>
public static class TargetingAcquisitionLogic {
  /// <summary>
  /// Whether a <see cref="Targetable"/> should appear when Tab-cycling targets on this client.
  /// Mirrors <see cref="TargetingController.CycleTarget"/>.
  /// </summary>
  public static bool IsTabTargetingCandidate(bool movementClaimsLocalInputAuthority, bool targetHealthSaysDead) {
    return !(movementClaimsLocalInputAuthority || targetHealthSaysDead);
  }

  /// <summary>
  /// Performs the same layered raycasts as runtime selection: Fusion physics scene while the runner
  /// is running, falling back to the default Unity physics scene when the Fusion scene misses or the
  /// runner is unavailable.
  /// </summary>
  public static Targetable TryPickSelectableAlongRay(
    in Ray     ray,
    float      maxRaycastDistance,
    NetworkRunner runner,
    out bool   hitSomething,
    out RaycastHit hitInfo) {
    hitSomething = false;
    hitInfo      = default;

    // QueryTriggerInteraction.Collide: networked mobs use a CapsuleCollider set to "Is Trigger" so
    // gameplay CharacterControllers are not squeezed by overlapping solid primitives, while targeting
    // rays still resolve the clickable proxy volume under the Fusion/default physics scenes.
    const int                     layerMask = Physics.DefaultRaycastLayers;
    const QueryTriggerInteraction triggers  = QueryTriggerInteraction.Collide;

    if (runner != null && runner.IsRunning) {
      var fusionScene = runner.GetPhysicsScene();
      hitSomething = fusionScene.Raycast(
        ray.origin,
        ray.direction,
        out hitInfo,
        maxRaycastDistance,
        layerMask,
        triggers);

      if (!hitSomething) {
        hitSomething = Physics.Raycast(
          ray,
          out hitInfo,
          maxRaycastDistance,
          layerMask,
          triggers);
      }
    } else {
      hitSomething = Physics.Raycast(
        ray,
        out hitInfo,
        maxRaycastDistance,
        layerMask,
        triggers);
    }

    Targetable selectable = hitSomething ? hitInfo.collider.GetComponentInParent<Targetable>() : null;
    return selectable;
  }
}
