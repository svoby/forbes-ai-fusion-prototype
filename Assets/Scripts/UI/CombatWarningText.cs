/// <summary>
/// Pure static mapping from <see cref="CombatFeedbackReason"/> to a short player-facing
/// warning string displayed in the central combat banner.
/// <para>
/// No runtime state, no <see cref="UnityEngine.MonoBehaviour"/>, no Fusion dependency —
/// the mapping is unit-testable in EditMode without a scene or runner.
/// </para>
/// </summary>
public static class CombatWarningText {
  /// <summary>
  /// Returns the player-facing warning string for <paramref name="reason"/>.
  /// Returns an empty string for <see cref="CombatFeedbackReason.None"/> and for
  /// reserved wire values that are never shown (<see cref="CombatFeedbackReason.CastInterruptedByMovement"/>,
  /// <see cref="CombatFeedbackReason.CastInterruptedByJump"/>).
  /// </summary>
  public static string ForReason(CombatFeedbackReason reason) {
    return reason switch {
      CombatFeedbackReason.NoTarget                      => "No target",
      CombatFeedbackReason.OutOfRange                    => "Out of range",
      CombatFeedbackReason.TargetDead                    => "Target is dead",
      CombatFeedbackReason.OnCooldown                    => "Spell is on cooldown",
      CombatFeedbackReason.GcdActive                     => "Spell not ready",
      CombatFeedbackReason.AlreadyCasting                => "Already casting",
      CombatFeedbackReason.CasterDead                    => "You are dead",
      CombatFeedbackReason.CastInterruptedByMovement     => "",
      CombatFeedbackReason.CastInterruptedByJump         => "",
      CombatFeedbackReason.CastInterruptedByNewSpell     => "Spell interrupted",
      CombatFeedbackReason.CastInterruptedByDeath        => "Interrupted: you died",
      CombatFeedbackReason.CastInterruptedInvalidTarget  => "Interrupted: target lost",
      CombatFeedbackReason.CastInterruptedManual         => "Cast interrupted",
      _                                                   => "",
    };
  }
}
