using Fusion;
using UnityEngine;

/// <summary>
/// Pure validation logic: checks all pre-conditions for casting a spell.
/// Called from <see cref="NetworkCombatController"/> on the state authority so
/// gameplay outcomes are never decided by client-submitted results.
/// </summary>
public static class CombatValidator {
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

    if (!targetId.IsValid || !runner.TryFindObject(targetId, out var targetObj)) {
      failReason = CombatFailReason.NoTarget;
      return false;
    }

    if (!targetObj.TryGetComponent(out targetHealth)) {
      failReason = CombatFailReason.NoTarget;
      return false;
    }

    if (targetHealth.IsDead) {
      failReason = CombatFailReason.TargetDead;
      return false;
    }

    float dist = Vector3.Distance(caster.position, targetObj.transform.position);
    if (dist > spell.RangeMeters) {
      failReason = CombatFailReason.OutOfRange;
      return false;
    }

    failReason = CombatFailReason.None;
    return true;
  }
}
