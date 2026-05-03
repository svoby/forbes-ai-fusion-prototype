using UnityEngine;

/// <summary>
/// Pure helpers for world-space target health bars. No gameplay or network logic.
/// </summary>
public static class TargetHealthBarLogic {
  /// <summary>
  /// Returns <see cref="Mathf.Clamp01"/> of <paramref name="currentHealth"/> / <paramref name="maxHealth"/>.
  /// Yields 0 when <paramref name="maxHealth"/> &lt;= 0 or <paramref name="currentHealth"/> &lt;= 0.
  /// </summary>
  public static float ComputeHealthFill(float currentHealth, float maxHealth) {
    if (maxHealth <= 0f || currentHealth <= 0f) {
      return 0f;
    }

    return Mathf.Clamp01(currentHealth / maxHealth);
  }

  /// <summary>
  /// Rotation whose forward looks from the bar toward the camera (typical billboard for a world-space Quad/UI canvas).
  /// When the bar and camera coincide, returns <paramref name="fallbackWhenNoDirection"/>.
  /// </summary>
  public static Quaternion ComputeBillboardRotation(
    Vector3 barWorldPosition,
    Vector3 cameraWorldPosition,
    Quaternion fallbackWhenNoDirection,
    Vector3 worldUp) {
    Vector3 toCamera = cameraWorldPosition - barWorldPosition;
    if (toCamera.sqrMagnitude < 1e-8f) {
      return fallbackWhenNoDirection;
    }

    return Quaternion.LookRotation(toCamera, worldUp);
  }

  /// <summary>
  /// Sizes a UI strip horizontally from the parent left edge using anchors (alternative to <c>Image.Type.Filled</c>).
  /// Returns the same normalized value as <see cref="ComputeHealthFill"/> applied to (<paramref name="currentHealth"/>, <paramref name="maxHealth"/>).
  /// Does nothing when <paramref name="strip"/> is null.
  /// </summary>
  public static float ApplyHorizontalHpAnchors(
    RectTransform strip,
    float currentHealth,
    float maxHealth) {
    float t = ComputeHealthFill(currentHealth, maxHealth);
    if (strip == null) {
      return t;
    }

    strip.anchorMin        = new Vector2(0f, 0f);
    strip.anchorMax        = new Vector2(t, 1f);
    strip.offsetMin        = Vector2.zero;
    strip.offsetMax        = Vector2.zero;
    strip.anchoredPosition = Vector2.zero;
    return t;
  }
}
