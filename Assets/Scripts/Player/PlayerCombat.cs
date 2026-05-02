using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// WoW-style slice: Tab cycles hostile targets, key <c>1</c> casts an instant spell in range (authority on target HP).
/// </summary>
public class PlayerCombat : NetworkBehaviour {
  public float SpellDamage = 15f;
  public float SpellRange = 12f;

  [Networked] public NetworkId TargetId { get; set; }

  Health _health;
  readonly List<Health> _targetsScratch = new List<Health>(8);
  NetworkButtons _prevButtons;

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

    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.TabTarget)) {
      TargetId = CombatTargetSelector.SelectNextAfter(Object, TargetId, _targetsScratch);
    }

    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.SpellPrimary)) {
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
