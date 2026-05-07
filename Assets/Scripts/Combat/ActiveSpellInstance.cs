using Fusion;
using UnityEngine;

/// <summary>Spell instance kind; governs simulation and presentation logic.</summary>
public enum SpellInstanceKind : byte {
  TargetedProjectile = 0,
}

/// <summary>
/// Describes one active spell presentation/gameplay instance replicated to all clients.
/// SpellId == 0 means the slot is empty. Used in <see cref="ActiveSpellInstanceRegistry"/>.
/// </summary>
public partial struct ActiveSpellInstance : INetworkStruct {
  /// <summary>0 = slot empty/completed; non-zero = active spell.</summary>
  public byte SpellId;

  /// <summary>Governs simulation and presentation routing.</summary>
  public SpellInstanceKind Kind;

  public NetworkId CasterId;
  public NetworkId TargetId;

  /// <summary>Caster world position at moment of release; used for visual arc.</summary>
  public Vector3 Origin;

  /// <summary>Simulation tick when this instance was created/released.</summary>
  public int ReleaseTick;

  public bool IsActive => SpellId != 0;
}
