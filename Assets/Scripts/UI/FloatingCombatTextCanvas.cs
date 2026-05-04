using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns a <see cref="RenderMode.ScreenSpaceOverlay"/> canvas used exclusively for
/// floating combat text numbers. Created lazily on the first call to
/// <see cref="ShowDamage"/>; lives for the session (DontDestroyOnLoad).
/// <para>
/// Cosmetic only: never writes gameplay state, no networked objects, no colliders.
/// Each connected client renders its own numbers independently.
/// </para>
/// <para>
/// Usage: <c>FloatingCombatTextCanvas.ShowDamage(targetTransform, damageAmount);</c>
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class FloatingCombatTextCanvas : MonoBehaviour {
  const float  TextLifetimeSec = 0.9f;
  const string CanvasObjectName = "FloatingCombatTextCanvas";

  static readonly Color DamageColor = new Color(1f, 0.15f, 0.15f);

  static FloatingCombatTextCanvas _instance;

  // ── Public API ────────────────────────────────────────────────────────────────

  /// <summary>
  /// Shows a floating damage number anchored above <paramref name="worldAnchor"/>'s
  /// collider bounds (or a fixed offset when no collider is present).
  /// <para>
  /// No-op when <see cref="Camera.main"/> is absent (headless mode, test environments).
  /// </para>
  /// </summary>
  public static void ShowDamage(Transform worldAnchor, float damage) {
    if (Camera.main == null) {
      return;
    }

    GetOrCreate().SpawnItem(worldAnchor, Mathf.RoundToInt(damage).ToString());
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────

  void OnDestroy() {
    if (_instance == this) {
      _instance = null;
    }
  }

  // ── Internal ──────────────────────────────────────────────────────────────────

  static FloatingCombatTextCanvas GetOrCreate() {
    if (_instance != null) {
      return _instance;
    }

    // Re-link after a domain reload or scene change if the object still exists.
    _instance = FindAnyObjectByType<FloatingCombatTextCanvas>();
    if (_instance != null) {
      return _instance;
    }

    var go = new GameObject(CanvasObjectName);
    DontDestroyOnLoad(go);

    var canvas = go.AddComponent<Canvas>();
    canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 500;

    // Prevent the canvas root from consuming pointer events.
    var rootGroup = go.AddComponent<CanvasGroup>();
    rootGroup.blocksRaycasts = false;
    rootGroup.interactable   = false;

    _instance = go.AddComponent<FloatingCombatTextCanvas>();
    return _instance;
  }

  void SpawnItem(Transform worldAnchor, string text) {
    Font font = CastBarView.ResolveDefaultHudFont(36);

    var go = new GameObject("FCT_Item");
    go.transform.SetParent(transform, worldPositionStays: false);

    var rt      = go.AddComponent<RectTransform>();
    rt.sizeDelta = new Vector2(160f, 48f);
    rt.pivot     = new Vector2(0.5f, 0f);

    var group = go.AddComponent<CanvasGroup>();
    group.blocksRaycasts = false;
    group.interactable   = false;

    var label                  = go.AddComponent<Text>();
    label.text                 = text;
    label.fontSize             = 32;
    label.fontStyle            = FontStyle.Bold;
    label.color                = DamageColor;
    label.alignment            = TextAnchor.MiddleCenter;
    label.horizontalOverflow   = HorizontalWrapMode.Overflow;
    label.verticalOverflow     = VerticalWrapMode.Overflow;
    label.raycastTarget        = false;
    if (font != null) {
      label.font = font;
    }

    var item = go.AddComponent<FloatingCombatTextItem>();
    item.Init(worldAnchor, text, TextLifetimeSec);
  }
}
