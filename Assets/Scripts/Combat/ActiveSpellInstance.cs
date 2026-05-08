using Fusion;
using UnityEngine;

/// <summary>Spell instance kind; governs simulation and presentation logic.</summary>
public enum SpellInstanceKind : byte {
  TargetedProjectile = 0,
}

/// <summary>
/// Describes one active spell presentation/gameplay instance replicated to all clients.
/// SpellId == 0 means the entry is inactive. Used in <see cref="ActiveSpellInstanceRegistry"/>.
/// </summary>
public partial struct ActiveSpellInstance : INetworkStruct {
  /// <summary>0 = inactive (no active spell); non-zero = active spell.</summary>
  public byte SpellId;

  /// <summary>Governs simulation and presentation routing.</summary>
  public SpellInstanceKind Kind;

  public NetworkId CasterId;
  public NetworkId TargetId;

  /// <summary>Caster world position at moment of release; used for visual arc.</summary>
  public Vector3 Origin;

  /// <summary>Simulation tick when this instance was created/released.</summary>
  public int ReleaseTick;

  /// <summary>
  /// Non-zero unique id assigned at creation by the state authority.
  /// Guards against visual reuse when a new spell occupies the same entry index.
  /// 0 = unassigned (struct default / inactive).
  /// </summary>
  public int InstanceId;

  public bool IsActive => SpellId != 0;
}
