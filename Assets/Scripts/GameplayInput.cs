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
/// </summary>
public struct GameplayInput : INetworkInput {
  public Vector2 Move;
  public NetworkButtons Buttons;
}
