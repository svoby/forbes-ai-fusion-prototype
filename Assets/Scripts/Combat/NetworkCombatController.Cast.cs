using Fusion;

public partial class NetworkCombatController {
  /// <summary>
  /// Authoritative cast interrupt. Only mutates networked cast fields on state authority.
  /// Does not refund GCD or alter spell cooldowns.
  /// </summary>
  public void TryCancelCast(CastCancelReason reason) {
    if (!HasStateAuthority || Runner == null) {
      return;
    }

    if (reason == CastCancelReason.None) {
      return;
    }

    if (reason == CastCancelReason.Death) {
      bool hadCast = IsCasting;
      ClearCastState();
      // Death clears both cast state and any in-flight active instances: the
      // projectile is abandoned and the target will not be damaged.
      // TODO: Gameplay policy — caster death may later differ by spell type
      // (e.g. a persistent AoE might survive the caster's death). Keep as-is for now.
      _registry?.RemoveAllForCaster(Object.Id);
      if (hadCast) {
        SetCombatFeedback(CombatFeedbackReason.CastInterruptedByDeath);
        ForbesLog.Net($"Cast cancelled: {reason}", this);
      }
      return;
    }

    // Non-Death cancellations clear cast state only, not pending impact.
    // Movement and jump are silent. Other reasons set feedback and log.
    if (!IsCasting) {
      return;
    }

    ClearCastState();
    if (reason == CastCancelReason.Movement || reason == CastCancelReason.Jump) {
      return;
    }

    var feedback = CombatFeedbackReasonMapping.FromCastCancel(reason);
    if (feedback != CombatFeedbackReason.None) {
      SetCombatFeedback(feedback);
    }
    ForbesLog.Net($"Cast cancelled: {reason}", this);
  }

  /// <summary>
  /// Request-level authoritative cast API shared by player input today and
  /// future AI/mob casters. This refactor keeps NetworkCombatController as the
  /// combat owner; it does not introduce a new combat system.
  /// </summary>
  public bool TryRequestCast(byte spellId, NetworkId targetId) {
    if (!HasStateAuthority || Runner == null) {
      return false;
    }

    if (IsCasting) {
      if (CurrentSpellId == spellId) {
        return false;
      }
      TryCancelCast(CastCancelReason.NewSpell);
    }

    return TryStartCast(spellId, targetId);
  }

  bool TryStartCast(byte spellId, NetworkId targetId) {
    var spell = SpellRegistry.Get(spellId);
    if (!spell.IsValid) {
      return false;
    }

    int cooldownEnd = GetCooldownEndTick(spellId);

    if (!CombatValidator.TryValidate(
          Runner, transform, targetId, spell,
          Runner.Tick, GcdEndTick, cooldownEnd,
          isAlreadyCasting: CurrentSpellId != 0,
          out var targetHealth, out var failReason)) {
      SetCombatFeedback(CombatFeedbackReasonMapping.FromValidatorFailure(failReason));
      ForbesLog.Net($"Cast rejected: {failReason} spell={spell.Name}", this);
      return false;
    }

    int castTicks = SecsToTicks(spell.CastTimeSec);

    if (castTicks == 0) {
      // Instant spell: GCD and cooldown start immediately.
      if (spell.TriggersGcd)
      {
        GcdEndTick = Runner.Tick + SecsToTicks(GcdSec);
        
      }
      SetCooldownEndTick(spellId, Runner.Tick + SecsToTicks(spell.CooldownSec));

      if (SpellTravelLogic.HasProjectile(spell)) {
        ScheduleProjectileInstance(spellId, targetId, spell.Name, "Instant cast");
      } else {
        targetHealth.DealDamageRpc(spell.Damage);
        DispatchImpactVisual(spellId, targetId);
      }
    } else {
      // Cast-time spell: GCD and cooldown are deferred to ResolveCast.
      CurrentSpellId = spellId;
      CastTarget     = targetId;
      CastStartTick  = Runner.Tick;
      CastEndTick    = Runner.Tick + castTicks;
      ForbesLog.Net($"Cast started: {spell.Name} castTicks={castTicks}", this);
    }

    return true;
  }

  void ResolveCast() {
    var spell = SpellRegistry.Get(CurrentSpellId);

    // Re-validate: target might have died or walked out of range during cast.
    if (!CombatValidator.TryValidate(
          Runner, transform, CastTarget, spell,
          Runner.Tick, gcdEndTick: 0, cooldownEndTick: 0,
          isAlreadyCasting: false,
          out var targetHealth, out var failReason)) {
      SetCombatFeedback(CombatFeedbackReasonMapping.FromValidatorFailure(failReason));
      ForbesLog.Net($"Cast resolved but invalid at completion: {failReason}", this);
      ClearCastState();
      return;
    }

    // Cast completed successfully: trigger GCD and cooldown now.
    if (spell.TriggersGcd) {
      GcdEndTick = Runner.Tick + SecsToTicks(GcdSec);
    }
    SetCooldownEndTick(CurrentSpellId, Runner.Tick + SecsToTicks(spell.CooldownSec));

    if (SpellTravelLogic.HasProjectile(spell)) {
      ScheduleProjectileInstance(CurrentSpellId, CastTarget, spell.Name, "Cast resolved");
    } else {
      targetHealth.DealDamageRpc(spell.Damage);
      DispatchImpactVisual(CurrentSpellId, CastTarget);
    }

    ClearCastState();
  }

  void ClearCastState() {
    CurrentSpellId = 0;
    CastTarget     = default;
    CastStartTick  = 0;
    CastEndTick    = 0;
  }

  // Constructs and registers a TargetedProjectile instance. Shared by instant and cast-time paths.
  void ScheduleProjectileInstance(byte spellId, NetworkId targetId, string spellName, string logPrefix) {
    var inst = new ActiveSpellInstance {
      SpellId     = spellId,
      Kind        = SpellInstanceKind.TargetedProjectile,
      CasterId    = Object.Id,
      TargetId    = targetId,
      Origin      = transform.position,
      ReleaseTick = Runner.Tick,
    };
    int entryIndex = _registry?.TryAdd(inst) ?? -1;
    if (entryIndex < 0) {
      ForbesLog.Warn(
        _registry == null
          ? $"Projectile instance dropped: no ActiveSpellInstanceRegistry on '{gameObject.name}'. spellId={spellId}"
          : $"Projectile instance dropped: registry full. spellId={spellId}",
        this);
    }
    ForbesLog.Net($"{logPrefix} (projectile): {spellName} releaseTick={Runner.Tick}", this);
  }
}
