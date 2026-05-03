using UnityEngine;

/// <summary>
/// Pure projectile travel math — no Fusion runner dependency.
/// Logical projectiles only; used by <see cref="NetworkCombatController"/> on state authority.
/// </summary>
public static class SpellTravelLogic {
  public static bool HasProjectile(SpellData spell) {
    return spell.ProjectileSpeedMetersPerSecond > 0f;
  }

  /// <summary>
  /// Computes simulation ticks until impact: ceil(distance / speed * tickRate), with guards.
  /// </summary>
  public static int ComputeTravelTicks(float distanceMeters, float speedMetersPerSecond, int tickRate) {
    if (speedMetersPerSecond <= 0f || tickRate <= 0) {
      return 0;
    }

    float d = distanceMeters < 0f ? 0f : distanceMeters;
    if (d <= 0f) {
      return 0;
    }

    return Mathf.CeilToInt(d / speedMetersPerSecond * tickRate);
  }

  public static int ComputeImpactTick(int releaseTick, int travelTicks) {
    return releaseTick + Mathf.Max(0, travelTicks);
  }
}
