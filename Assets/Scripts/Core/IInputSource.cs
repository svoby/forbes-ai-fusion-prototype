using UnityEngine;

/// <summary>
/// Source of player intent for one tick. Implementations live next to the runner
/// (<c>KeyboardInputSource</c>) and are sampled by <c>FusionInputProvider</c> from
/// <c>OnInput</c>. Edge presses are consumed exactly once.
/// </summary>
public interface IInputSource {
  Vector2 MoveAxes { get; }

  /// <summary>Local view yaw in degrees, used by simulation to face the player.</summary>
  float LookYaw { get; }

  bool ConsumeJump();
  bool ConsumeTabTarget();
  bool ConsumeSpellPrimary();
  bool ConsumeRandomizeColor();
}
