using Fusion;
using UnityEngine;

/// <summary>
/// Pure validation logic: checks all pre-conditions for casting a spell.
/// Called from <see cref="NetworkCombatController"/> on the state authority so
/// gameplay outcomes are never decided by client-submitted results.
/// </summary>
public static class CombatValidator {
  /// <summary>
  /// Runner-aware entry point. Resolves <paramref name="targetId"/> to a sibling
  /// <see cref="Health"/> via the runner, then forwards to the pure overload so
  /// the rejection-order rules live in exactly one place.
  /// </summary>
  public static bool TryValidate(
    NetworkRunner runner,
    Transform     caster,
    NetworkId     targetId,
    SpellData     spell,
    int           currentTick,
    int           gcdEndTick,
    int           cooldownEndTick,
    bool          isAlreadyCasting,
    out Health    targetHealth,
    out CombatFailReason failReason) {

    targetHealth = null;
    Transform targetTransform = null;
    bool      isTargetDead    = false;

    // Resolve the target up front so the pure overload can stay independent of
    // the runner. If any step fails we leave targetTransform null and let the
    // pure rules surface NoTarget at the right place in the order.
    if (targetId.IsValid
        && runner != null
        && runner.TryFindObject(targetId, out var targetObj)
        && targetObj != null
        && targetObj.TryGetComponent(out targetHealth)) {
      targetTransform = targetObj.transform;
      isTargetDead    = targetHealth.IsDead;
    }

    return TryValidate(
      caster, targetId, spell,
      currentTick, gcdEndTick, cooldownEndTick,
      isAlreadyCasting,
      targetTransform, isTargetDead,
      out failReason);
  }

  /// <summary>
  /// Pure rule core: no <see cref="NetworkRunner"/> dependency. Callers must
  /// pre-resolve the target's <see cref="Transform"/> (null = unresolved /
  /// missing <see cref="Health"/>) and dead-state. Used by the runner-aware
  /// overload above and by EditMode tests; never call this from gameplay code
  /// without first resolving via the runner.
  /// </summary>
  public static bool TryValidate(
    Transform caster,
    NetworkId targetId,
    SpellData spell,
    int       currentTick,
    int       gcdEndTick,
    int       cooldownEndTick,
    bool      isAlreadyCasting,
    Transform targetTransform,
    bool      isTargetDead,
    out CombatFailReason failReason) {

    if (isAlreadyCasting) {
      failReason = CombatFailReason.AlreadyCasting;
      return false;
    }

    if (spell.TriggersGcd && currentTick < gcdEndTick) {
      failReason = CombatFailReason.GcdActive;
      return false;
    }

    if (currentTick < cooldownEndTick) {
      failReason = CombatFailReason.OnCooldown;
      return false;
    }

    if (!targetId.IsValid || targetTransform == null) {
      failReason = CombatFailReason.NoTarget;
      return false;
    }

    if (isTargetDead) {
      failReason = CombatFailReason.TargetDead;
      return false;
    }

    float dist = Vector3.Distance(caster.position, targetTransform.position);
    if (dist > spell.RangeMeters) {
      failReason = CombatFailReason.OutOfRange;
      return false;
    }

    failReason = CombatFailReason.None;
    return true;
  }
}
