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
      CycleTarget();
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

  void CollectAliveOthers(List<Health> into) {
    into.Clear();
    foreach (var h in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) {
      if (h.Object == null || h.Object == Object || h.IsDead) {
        continue;
      }

      into.Add(h);
    }

    into.Sort(static (a, b) => a.Object.Id.Raw.CompareTo(b.Object.Id.Raw));
  }

  void CycleTarget() {
    CollectAliveOthers(_targetsScratch);
    if (_targetsScratch.Count == 0) {
      TargetId = default;
      return;
    }

    int idx = 0;
    if (TargetId.IsValid) {
      idx = _targetsScratch.FindIndex(h => h.Object.Id == TargetId);
      if (idx < 0) {
        idx = 0;
      } else {
        idx = (idx + 1) % _targetsScratch.Count;
      }
    }

    TargetId = _targetsScratch[idx].Object.Id;
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
