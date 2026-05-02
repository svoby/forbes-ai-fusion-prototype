using Fusion;
using UnityEngine;

public enum GameplayButtons {
  Jump = 0,
  TabTarget = 1,
  SpellPrimary = 2,
  RandomizeColor = 3,
}

/// <summary>
/// Player intent for the current tick (Fusion <see cref="INetworkInput"/>).
/// <para>
/// <see cref="LookYaw"/> carries the local view yaw in degrees so simulation can
/// face/move the player without sampling render-time camera state inside
/// <c>FixedUpdateNetwork</c>.
/// </para>
/// </summary>
public struct GameplayInput : INetworkInput {
  public Vector2 Move;
  public NetworkButtons Buttons;
  public float LookYaw;
}
