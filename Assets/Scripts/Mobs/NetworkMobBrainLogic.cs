using UnityEngine;

/// <summary>
/// Pure helpers for <see cref="NetworkMobBrain"/> (unit-tested from EditMode).
/// </summary>
public enum NetworkMobBrainState {
  Idle,
  Wander,
}

public static class NetworkMobBrainLogic {
  /// <summary>
  /// Uniform random point on the XZ disk around <paramref name="origin"/>; Y matches origin.
  /// <paramref name="uRadius"/> and <paramref name="uAngle"/> must be in [0, 1).
  /// </summary>
  public static Vector3 PickDestinationXZ(Vector3 origin, float radius, float uRadius, float uAngle) {
    if (radius <= 0f) {
      return origin;
    }

    uRadius = Mathf.Clamp01(uRadius);
    uAngle = Mathf.Repeat(uAngle, 1f);
    float angle = uAngle * Mathf.PI * 2f;
    float r = Mathf.Sqrt(uRadius) * radius;
    return new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y, origin.z + Mathf.Sin(angle) * r);
  }

  public static float HorizontalSqrDistance(Vector3 a, Vector3 b) {
    float dx = a.x - b.x;
    float dz = a.z - b.z;
    return dx * dx + dz * dz;
  }

  public static bool ShouldLeaveIdle(NetworkMobBrainState state, int tick, int idleUntilTick) {
    return state == NetworkMobBrainState.Idle && tick >= idleUntilTick;
  }

  public static bool HasArrivedHorizontally(Vector3 position, Vector3 destination, float arrivalThreshold) {
    float th = Mathf.Max(0f, arrivalThreshold);
    return HorizontalSqrDistance(position, destination) <= th * th;
  }
}
