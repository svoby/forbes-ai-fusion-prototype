using UnityEngine;

/// <summary>
/// Pure helpers for <see cref="NetworkMobBrain"/> (unit-tested from EditMode).
/// </summary>
public enum NetworkMobBrainState {
  Idle,
  Wander,
}

public static class NetworkMobBrainLogic {
  const float HorizontalEpsSqr = 1e-8f;

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

  /// <summary>
  /// Normalized direction on XZ from <paramref name="from"/> to <paramref name="to"/>; Y difference is ignored.
  /// Returns false and <paramref name="horizontalDirection"/> zero when horizontal separation is below epsilon.
  /// </summary>
  public static bool TryGetHorizontalDirection(Vector3 from, Vector3 to, out Vector3 horizontalDirection) {
    float dx = to.x - from.x;
    float dz = to.z - from.z;
    float sqr = dx * dx + dz * dz;
    if (sqr < HorizontalEpsSqr || float.IsNaN(sqr) || float.IsInfinity(sqr)) {
      horizontalDirection = Vector3.zero;
      return false;
    }

    float invLen = 1f / Mathf.Sqrt(sqr);
    horizontalDirection = new Vector3(dx * invLen, 0f, dz * invLen);
    return true;
  }

  /// <summary>
  /// Instant yaw to face <paramref name="horizontalForward"/> projected onto XZ.
  /// When the horizontal length is near zero or non-finite, returns <paramref name="fallback"/>.
  /// </summary>
  public static Quaternion RotationFacingHorizontal(Vector3 horizontalForward, Quaternion fallback) {
    float x = horizontalForward.x;
    float z = horizontalForward.z;
    float sqr = x * x + z * z;
    if (sqr < HorizontalEpsSqr || float.IsNaN(sqr) || float.IsInfinity(sqr)) {
      return fallback;
    }

    float invLen = 1f / Mathf.Sqrt(sqr);
    Vector3 forward = new Vector3(x * invLen, 0f, z * invLen);
    if (float.IsNaN(forward.x) || float.IsInfinity(forward.x)) {
      return fallback;
    }

    return Quaternion.LookRotation(forward, Vector3.up);
  }
}
