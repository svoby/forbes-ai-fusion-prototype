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

  /// <summary>
  /// Advances a homing missile one simulation step toward the target's current position.
  /// Returns the new missile position. Returns <paramref name="missilePos"/> unchanged when
  /// <paramref name="speedMPS"/> or <paramref name="deltaTime"/> are non-positive.
  /// </summary>
  public static Vector3 AdvanceMissilePosition(
    Vector3 missilePos, Vector3 targetPos, float speedMPS, float deltaTime) {
    float step = speedMPS * deltaTime;
    return step <= 0f ? missilePos : Vector3.MoveTowards(missilePos, targetPos, step);
  }

  /// <summary>
  /// Returns true when the missile is within one simulation step of the target —
  /// i.e. it will reach the target this tick. Always false when speed or deltaTime
  /// are non-positive.
  /// </summary>
  public static bool HasMissileArrived(
    Vector3 missilePos, Vector3 targetPos, float speedMPS, float deltaTime) {
    float step = speedMPS * deltaTime;
    return step > 0f && Vector3.Distance(missilePos, targetPos) <= step;
  }
}
