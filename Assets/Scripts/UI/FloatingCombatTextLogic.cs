using UnityEngine;

/// <summary>
/// Pure stateless math helpers for floating combat text.
/// No scene setup, no MonoBehaviour, no gameplay state.
/// All methods are factored out here for EditMode unit-testing without a runner.
/// </summary>
public static class FloatingCombatTextLogic {
  /// <summary>
  /// Alpha for a floating text item at <paramref name="elapsed"/> seconds into its lifetime.
  /// Full opacity for the first 60 % of lifetime, then a linear fade to zero.
  /// </summary>
  public static float ComputeAlpha(float elapsed, float lifetime) {
    if (lifetime <= 0f) {
      return 0f;
    }

    float t = Mathf.Clamp01(elapsed / lifetime);
    const float FadeStart = 0.6f;

    if (t <= FadeStart) {
      return 1f;
    }

    return 1f - (t - FadeStart) / (1f - FadeStart);
  }

  /// <summary>
  /// Upward pixel offset at <paramref name="elapsed"/> seconds.
  /// Uses an ease-out curve: fast at the start, slowing near the end.
  /// </summary>
  public static float ComputePixelOffset(float elapsed, float lifetime, float maxPixels) {
    if (lifetime <= 0f) {
      return maxPixels;
    }

    float t  = Mathf.Clamp01(elapsed / lifetime);
    float ot = 1f - t;
    return maxPixels * (1f - ot * ot);
  }

  /// <summary>
  /// Returns <c>true</c> when <paramref name="screenPoint"/> is behind the camera
  /// (z &lt;= 0) and the item should be hidden.
  /// </summary>
  public static bool IsBehindCamera(Vector3 screenPoint) => screenPoint.z <= 0f;

  /// <summary>
  /// World-space anchor above the target's head.
  /// Uses collider bounds when available; falls back to a fixed vertical offset.
  /// </summary>
  public static Vector3 GetWorldAnchor(
    Transform target,
    float     colliderTopOffset = 0.25f,
    float     fallbackUpOffset  = 2.3f) {
    if (target == null) {
      return Vector3.zero;
    }

    if (target.TryGetComponent<Collider>(out var col) && col.enabled) {
      var b = col.bounds;
      return new Vector3(b.center.x, b.max.y + colliderTopOffset, b.center.z);
    }

    return target.position + Vector3.up * fallbackUpOffset;
  }
}
