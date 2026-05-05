/// <summary>
/// Player-facing combat warning replicated on <see cref="NetworkCombatController"/>.
/// Values 0–7 align with <see cref="CombatFailReason"/> (cast reject / resolution-fail).
/// Values 8–9 are reserved wire slots (legacy); gameplay never emits them — movement/jump
/// cancels are silent (no HUD, no log).
/// Values 10+ are other cast interrupts (see <see cref="CastCancelReason"/>).
/// </summary>
public enum CombatFeedbackReason : byte {
  None = 0,
  NoTarget = 1,
  OutOfRange = 2,
  TargetDead = 3,
  OnCooldown = 4,
  GcdActive = 5,
  AlreadyCasting = 6,
  CasterDead = 7,

  /// <summary>Reserved wire value; not used — movement cancel has no player feedback.</summary>
  CastInterruptedByMovement = 8,
  /// <summary>Reserved wire value; not used — jump cancel has no player feedback.</summary>
  CastInterruptedByJump = 9,
  CastInterruptedByNewSpell = 10,
  CastInterruptedByDeath = 11,
  CastInterruptedInvalidTarget = 12,
  CastInterruptedManual = 13,
}

/// <summary>Maps validator output and local cancel reasons into HUD/wire feedback.</summary>
public static class CombatFeedbackReasonMapping {
  public static CombatFeedbackReason FromValidatorFailure(CombatFailReason reason) {
    return (CombatFeedbackReason)(byte)reason;
  }

  public static CombatFeedbackReason FromCastCancel(CastCancelReason reason) {
    return reason switch {
      CastCancelReason.Movement      => CombatFeedbackReason.None,
      CastCancelReason.Jump          => CombatFeedbackReason.None,
      CastCancelReason.NewSpell      => CombatFeedbackReason.CastInterruptedByNewSpell,
      CastCancelReason.Death         => CombatFeedbackReason.CastInterruptedByDeath,
      CastCancelReason.InvalidTarget => CombatFeedbackReason.CastInterruptedInvalidTarget,
      CastCancelReason.Manual        => CombatFeedbackReason.CastInterruptedManual,
      _                              => CombatFeedbackReason.None,
    };
  }
}
