/// <summary>Why a spell cast was rejected by the state authority.</summary>
public enum CombatFailReason : byte {
  None          = 0,
  NoTarget      = 1,
  OutOfRange    = 2,
  TargetDead    = 3,
  OnCooldown    = 4,
  GcdActive     = 5,
  AlreadyCasting = 6,
  /// <summary>
  /// Reserved — byte value pinned for wire compatibility.
  /// No production code path emits this value today; runtime caster-death
  /// interrupts use <see cref="CastCancelReason.Death"/> instead.
  /// </summary>
  CasterDead    = 7,
}
