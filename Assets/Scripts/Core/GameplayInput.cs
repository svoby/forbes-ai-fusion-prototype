using Fusion;
using UnityEngine;

public enum GameplayButtons {
  Jump           = 0,
  Spell1         = 1,
  Spell2         = 2,
  Spell3         = 3,
  // 4 = reserved
  AlwaysFaceYaw  = 5,  // set every tick when RMB/Both mouse mode is active; simulation faces LookYaw regardless of movement direction
  RandomizeColor = 6,
}

/// <summary>
/// Player intent for the current tick (<see cref="INetworkInput"/>).
/// <para>
/// <see cref="LookYaw"/> is the camera's current yaw in degrees. The character
/// always faces this direction on the simulation side.
/// </para>
/// <para>
/// <see cref="TargetId"/> carries the locally selected target every tick so the
/// state authority can validate it at cast time without a separate RPC.
/// </para>
/// </summary>
public struct GameplayInput : INetworkInput {
  public Vector2         Move;
  public NetworkButtons  Buttons;
  public float           LookYaw;
  public NetworkId       TargetId;  // local client selection; 0/default = no target
}
