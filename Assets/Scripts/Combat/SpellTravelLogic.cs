using UnityEngine;

/// <summary>
/// Pure projectile travel math — no Fusion runner dependency.
/// Logical projectiles only; used by <see cref="NetworkCombatController"/> on state authority.
/// <para>Public surface:</para>
/// <list type="bullet">
/// <item><see cref="HasProjectile"/> — true when a spell has a projectile speed.</item>
/// <item><see cref="ComputeTravelTicks"/> — retained utility / tests; not the active resolution mechanism.</item>
/// <item><see cref="ComputeImpactTick"/> — retained utility / tests; not the active resolution mechanism.</item>
/// <item><see cref="AdvanceMissilePosition"/> — per-tick homing step used by NCC.</item>
/// <item><see cref="HasMissileArrived"/> — per-tick impact gate used by NCC.</item>
/// </list>
/// </summary>
public static class SpellTravelLogic {
  public static bool HasProjectile(SpellData spell) {
    return spell.ProjectileSpeedMetersPerSecond > 0f;
  }

  /// <summary>
  /// Computes simulation ticks until impact: ceil(distance / speed * tickRate), with guards.
  /// <para>
  /// Retained as a utility and for EditMode tests.
  /// <b>Not called by <see cref="NetworkCombatController"/>.</b>
  /// Missile resolution now uses <see cref="AdvanceMissilePosition"/> and
  /// <see cref="HasMissileArrived"/> per simulation tick.
  /// </para>
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

  /// <summary>
  /// Returns <paramref name="releaseTick"/> + max(0, <paramref name="travelTicks"/>).
  /// <para>
  /// Retained as a utility and for EditMode tests.
  /// <b>Not called by <see cref="NetworkCombatController"/>.</b>
  /// Missile resolution now uses <see cref="AdvanceMissilePosition"/> and
  /// <see cref="HasMissileArrived"/> per simulation tick.
  /// </para>
  /// </summary>
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
  /// Returns true when the missile's distance to the target is within one
  /// simulation step. The check is intentionally designed to be called with the
  /// <b>post-advance</b> position (as <see cref="NetworkCombatController"/> does):
  /// advance first, then call this; a missile that was between 1× and 2× one
  /// step away before advancing will arrive on the same tick it advances.
  /// Always returns false when speed or deltaTime are non-positive.
  /// </summary>
  public static bool HasMissileArrived(
    Vector3 missilePos, Vector3 targetPos, float speedMPS, float deltaTime) {
    float step = speedMPS * deltaTime;
    return step > 0f && Vector3.Distance(missilePos, targetPos) <= step;
  }
}
