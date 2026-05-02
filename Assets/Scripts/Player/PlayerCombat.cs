using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// LEGACY — will be replaced by <see cref="NetworkCombatController"/> in Milestone 3.
/// Kept temporarily so the PlayerCharacter prefab doesn't lose a component mid-development.
/// Tab-targeting removed (moved to local <see cref="TargetingController"/>).
/// </summary>
public class PlayerCombat : NetworkBehaviour {
  public float SpellDamage = 15f;
  public float SpellRange  = 12f;

  [Networked] public NetworkId TargetId { get; set; }

  Health                _health;
  readonly List<Health> _targetsScratch = new List<Health>(8);
  NetworkButtons        _prevButtons;

  void Awake() {
    _health = GetComponent<Health>();
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority || _health == null || _health.IsDead) {
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    PruneInvalidTarget();

    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell1)) {
      TryCastSpell();
    }

    _prevButtons = input.Buttons;
  }

  void PruneInvalidTarget() {
    if (!TargetId.IsValid) {
      return;
    }

    if (!Runner.TryFindObject(TargetId, out var obj) || !obj.TryGetComponent(out Health h) || h.IsDead || h.Object == Object) {
      TargetId = default;
    }
  }

  void TryCastSpell() {
    if (!TargetId.IsValid || !Runner.TryFindObject(TargetId, out var obj) || !obj.TryGetComponent(out Health target) || target.IsDead) {
      return;
    }

    float dist = Vector3.Distance(transform.position, target.transform.position);
    if (dist > SpellRange) {
      return;
    }

    target.DealDamageRpc(SpellDamage);
  }
}
