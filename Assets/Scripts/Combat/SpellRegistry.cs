/// <summary>
/// Compile-time spell data record. Stored in <see cref="SpellRegistry.All"/> indexed
/// by <c>SpellId - 1</c> (SpellId is 1-based; 0 means "no spell").
/// Cast/cooldown times in seconds are converted to Fusion ticks at runtime so timers
/// are tick-accurate and survive host migration.
/// </summary>
public readonly struct SpellData {
  public readonly byte   Id;
  public readonly string Name;
  public readonly float  CastTimeSec;
  public readonly float  CooldownSec;
  public readonly float  RangeMeters;
  public readonly float  Damage;
  public readonly bool   TriggersGcd;

  public bool IsValid => Id != 0;

  public SpellData(byte id, string name, float castTimeSec, float cooldownSec, float rangeMeters, float damage, bool triggersGcd) {
    Id           = id;
    Name         = name;
    CastTimeSec  = castTimeSec;
    CooldownSec  = cooldownSec;
    RangeMeters  = rangeMeters;
    Damage       = damage;
    TriggersGcd  = triggersGcd;
  }
}

/// <summary>
/// Hardcoded spell table for the prototype. Both client and server resolve spell
/// properties by index so the network only ships the 1-byte spell ID.
/// </summary>
public static class SpellRegistry {
  public static readonly SpellData[] All = {
    new(1, "Fireball",    castTimeSec: 1.5f, cooldownSec: 0f, rangeMeters: 30f, damage: 30f, triggersGcd: true),
    new(2, "Arcane Shot", castTimeSec: 0f,   cooldownSec: 3f, rangeMeters: 25f, damage: 15f, triggersGcd: true),
    new(3, "Heavy Blast", castTimeSec: 2.5f, cooldownSec: 8f, rangeMeters: 30f, damage: 60f, triggersGcd: true),
  };

  /// <summary>Returns the spell for the given 1-based ID, or an invalid default if not found.</summary>
  public static SpellData Get(byte id) {
    if (id < 1 || id > All.Length) {
      return default;
    }
    return All[id - 1];
  }
}
