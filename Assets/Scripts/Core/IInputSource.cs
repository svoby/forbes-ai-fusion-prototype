using Fusion;
using UnityEngine;

/// <summary>
/// Source of player intent for one tick. Implementations live next to the runner
/// (<see cref="KeyboardInputSource"/>) and are sampled by <see cref="FusionInputProvider"/>
/// from <c>OnInput</c>. Edge presses are consumed exactly once.
/// </summary>
public interface IInputSource {
  Vector2 MoveAxes   { get; }

  /// <summary>Local view yaw in degrees; always equals <see cref="ThirdPersonOrbitCamera.Yaw"/>.</summary>
  float LookYaw { get; }

  /// <summary>
  /// True when RMB or both mouse buttons are held. The simulation uses this to force the
  /// character to face <see cref="LookYaw"/> every tick regardless of movement direction.
  /// </summary>
  bool AlwaysFaceYaw { get; }

  bool ConsumeJump();
  bool ConsumeSpell1();
  bool ConsumeSpell2();
  bool ConsumeSpell3();
  bool ConsumeRandomizeColor();
}
