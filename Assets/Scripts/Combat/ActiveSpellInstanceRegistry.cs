using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative fixed-capacity (16) registry of active spell instances for one caster.
/// Replaces the old single-slot PlayerMissileSlot with a fixed-capacity multi-instance model.
/// <para>
/// Replicated four-field flight descriptors allow any client to reconstruct
/// in-flight visuals without a networked projectile position. Authority-only
/// _virtualPositions advance homing logic per tick.
/// </para>
/// <para>
/// Execution order: [DefaultExecutionOrder(-200)] ensures per-tick instance
/// resolution runs before NetworkCombatController (-100) schedules new casts.
/// </para>
/// </summary>
[DefaultExecutionOrder(-200)]
public class ActiveSpellInstanceRegistry : NetworkBehaviour {
  public const int Capacity = 16;

  [Networked, Capacity(Capacity)]
  public NetworkArray<ActiveSpellInstance> Instances { get; }

  readonly Vector3[] _virtualPositions = new Vector3[Capacity];

  /// <summary>Fired on state authority when a projectile instance arrives at its target.</summary>
  public event Action<int, ActiveSpellInstance> OnInstanceArrived;

  /// <summary>Fired on state authority when a projectile instance is cancelled (target gone/dead).</summary>
  public event Action<int, ActiveSpellInstance, CombatFeedbackReason> OnInstanceCancelled;

  public override void Spawned() {
    if (HasStateAuthority) {
      for (int i = 0; i < Capacity; i++) {
        if (Instances[i].IsActive) {
          _virtualPositions[i] = Instances[i].Origin;
          ForbesLog.Net(
            $"Spawned with instance in flight at slot {i} — re-init virtualPos={_virtualPositions[i]}", this);
        }
      }
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    for (int i = 0; i < Capacity; i++) {
      if (Instances[i].IsActive) {
        TickInstance(i);
      }
    }
  }

  /// <summary>Authority: add a new active instance. Returns slot index, or -1 if registry is full.</summary>
  public int TryAdd(ActiveSpellInstance instance) {
    if (!HasStateAuthority) {
      return -1;
    }

    for (int i = 0; i < Capacity; i++) {
      if (!Instances[i].IsActive) {
        Instances.Set(i, instance);
        _virtualPositions[i] = instance.Origin;
        return i;
      }
    }

    ForbesLog.Warn("ActiveSpellInstanceRegistry: all slots full — instance dropped.", this);
    return -1;
  }

  /// <summary>Authority: mark a slot as inactive (SpellId = 0).</summary>
  public void Complete(int slotIndex) {
    if (!HasStateAuthority) {
      return;
    }

    if ((uint)slotIndex >= Capacity) {
      return;
    }

    Instances.Set(slotIndex, default);
  }

  /// <summary>Authority: cancel all active instances for a caster (e.g. on caster death).</summary>
  public void RemoveAllForCaster(NetworkId casterId) {
    if (!HasStateAuthority) {
      return;
    }

    for (int i = 0; i < Capacity; i++) {
      if (Instances[i].IsActive && Instances[i].CasterId == casterId) {
        Complete(i);
      }
    }
  }

  /// <summary>Returns true if any slot holds an active instance from the given caster.</summary>
  public bool HasActiveInstanceForCaster(NetworkId casterId) {
    for (int i = 0; i < Capacity; i++) {
      if (Instances[i].IsActive && Instances[i].CasterId == casterId) {
        return true;
      }
    }

    return false;
  }

  /// <summary>Returns the first active instance for a caster, or a default (inactive) struct.</summary>
  public ActiveSpellInstance GetFirstActiveInstanceForCaster(NetworkId casterId) {
    for (int i = 0; i < Capacity; i++) {
      var inst = Instances[i];
      if (inst.IsActive && inst.CasterId == casterId) {
        return inst;
      }
    }

    return default;
  }

  // Per-tick homing logic for one slot (state authority only).
  void TickInstance(int i) {
    var inst  = Instances[i];
    var spell = SpellRegistry.Get(inst.SpellId);

    if (!spell.IsValid) {
      Complete(i);
      return;
    }

    if (!Runner.TryFindObject(inst.TargetId, out var targetObj)
        || targetObj == null
        || !targetObj.TryGetComponent(out Health impactHealth)) {
      ForbesLog.Net($"Spell instance slot={i} spellId={inst.SpellId}: target missing -> NoTarget", this);
      var cancelled = inst;
      Complete(i);
      OnInstanceCancelled?.Invoke(i, cancelled, CombatFeedbackReason.NoTarget);
      return;
    }

    if (impactHealth.IsDead) {
      ForbesLog.Net($"Spell instance slot={i} spellId={inst.SpellId}: target dead -> TargetDead", this);
      var cancelled = inst;
      Complete(i);
      OnInstanceCancelled?.Invoke(i, cancelled, CombatFeedbackReason.TargetDead);
      return;
    }

    Vector3 targetPos    = targetObj.transform.position;
    float   speed        = spell.ProjectileSpeedMetersPerSecond;
    float   dt           = Runner.DeltaTime;
    _virtualPositions[i] = SpellTravelLogic.AdvanceMissilePosition(
      _virtualPositions[i], targetPos, speed, dt);

    if (!SpellTravelLogic.HasMissileArrived(_virtualPositions[i], targetPos, speed, dt)) {
      return;
    }

    var arrived = inst;
    Complete(i);
    OnInstanceArrived?.Invoke(i, arrived);
  }
}
