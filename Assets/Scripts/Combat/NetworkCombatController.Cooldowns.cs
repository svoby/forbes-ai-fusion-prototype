using UnityEngine;

public partial class NetworkCombatController {
  int GetCooldownEndTick(byte spellId) {
    return spellId switch {
      1 => Cooldown1EndTick,
      2 => Cooldown2EndTick,
      3 => Cooldown3EndTick,
      _ => 0,
    };
  }

  void SetCooldownEndTick(byte spellId, int tick) {
    switch (spellId) {
      case 1: Cooldown1EndTick = tick; break;
      case 2: Cooldown2EndTick = tick; break;
      case 3: Cooldown3EndTick = tick; break;
    }
  }

  /// <summary>Converts seconds to tick count using the runner's fixed tick rate.</summary>
  int SecsToTicks(float seconds) {
    return SecsToTicks(Runner.TickRate, seconds);
  }

  /// <summary>
  /// Pure tick-rounding helper exposed for EditMode tick-math tests. Must match
  /// the instance overload above exactly: gameplay timers depend on it.
  /// </summary>
  internal static int SecsToTicks(int tickRate, float seconds) {
    return Mathf.CeilToInt(seconds * tickRate);
  }
}
