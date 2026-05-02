using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local-only target selection. Created automatically at runtime if not already in the scene.
/// <see cref="FusionInputProvider"/> reads <see cref="CurrentTargetId"/> and includes it in
/// <see cref="GameplayInput.TargetId"/> every tick so the state authority can validate it.
/// <para>
/// Selection rules:
/// <list type="bullet">
///   <item>Tab — cycles through alive <see cref="Targetable"/> objects (closest-first), skipping the local player.</item>
///   <item>LMB click (no drag) — raycasts for a <see cref="Targetable"/>; selects it or keeps current if miss.</item>
///   <item>Escape — clears target.</item>
/// </list>
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class TargetingController : MonoBehaviour {
  [SerializeField] float _maxRaycastDistance = 100f;

  static readonly List<Targetable> _tabScratch = new List<Targetable>(16);

  ThirdPersonOrbitCamera _camera;
  Targetable             _currentTarget;

  public Targetable CurrentTarget  => _currentTarget;
  public NetworkId  CurrentTargetId =>
    _currentTarget != null && _currentTarget.NetworkObject != null
      ? _currentTarget.NetworkObject.Id
      : default;

  // ---- Auto-bootstrap ----

  /// <summary>
  /// Creates a TargetingController (and its TargetHighlight sibling) automatically
  /// at scene load if one doesn't already exist.
  /// </summary>
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void AutoCreate() {
    if (Object.FindAnyObjectByType<TargetingController>() != null) {
      return;
    }

    var go = new GameObject("[TargetingSystem]");
    go.AddComponent<LineRenderer>();    // must be before TargetHighlight.Awake
    go.AddComponent<TargetHighlight>();
    go.AddComponent<TargetingController>();
    Debug.Log("[TargetingController] Auto-created TargetingSystem. Run 'Apply Full Combat Setup' to persist.");
  }

  // ---- Per-frame logic ----

  bool _loggedStart;

  void Update() {
    if (!_loggedStart) {
      _loggedStart = true;
      Debug.Log($"[TargetingController] Running on '{gameObject.name}'. TargetHighlight.Instance={(TargetHighlight.Instance != null ? "OK" : "NULL")}");
    }

    EnsureCamera();

    var kb    = Keyboard.current;
    var mouse = Mouse.current;

    // Tab: cycle to next alive target.
    if (kb != null && kb.tabKey.wasPressedThisFrame) {
      Debug.Log("[TargetingController] Tab pressed.");
      CycleTarget();
    }

    // Escape: clear target.
    if (kb != null && kb.escapeKey.wasPressedThisFrame) {
      SetTarget(null);
    }

    // LMB release without drag: raycast select.
    if (mouse != null && mouse.leftButton.wasReleasedThisFrame) {
      bool dragged = _camera != null && _camera.IsLmbDragging;
      if (!dragged) {
        TrySelectFromScreenRay();
      }
    }
  }

  // ---- Tab cycle ----

  void CycleTarget() {
    _tabScratch.Clear();

    var allTargetables = Object.FindObjectsByType<Targetable>(FindObjectsInactive.Exclude);
    Debug.Log($"[TargetingController] CycleTarget: found {allTargetables.Length} Targetable(s) in scene.");

    foreach (var t in allTargetables) {
      var pm = t.GetComponent<PlayerMovement>();
      if (pm != null && pm.HasInputAuthority) {
        Debug.Log($"[TargetingController]   Skipping '{t.name}' (local player).");
        continue;
      }

      if (t.TryGetComponent(out Health h) && h.IsDead) {
        Debug.Log($"[TargetingController]   Skipping '{t.name}' (dead).");
        continue;
      }

      Debug.Log($"[TargetingController]   Candidate: '{t.DisplayName}'");
      _tabScratch.Add(t);
    }

    if (_tabScratch.Count == 0) {
      Debug.LogWarning("[TargetingController] CycleTarget: no valid candidates. Is Targetable on prefabs?");
      SetTarget(null);
      return;
    }

    // Stable sort by NetworkId so cycling order is deterministic.
    // NetworkId.Raw is uint; compare as uint to avoid signed-overflow issues.
    _tabScratch.Sort((a, b) => {
      uint aRaw = a.NetworkObject != null ? a.NetworkObject.Id.Raw : 0u;
      uint bRaw = b.NetworkObject != null ? b.NetworkObject.Id.Raw : 0u;
      return aRaw.CompareTo(bRaw);
    });

    // Find the current target and advance by one.
    int idx = 0;
    if (_currentTarget != null && _currentTarget.NetworkObject != null) {
      int found = _tabScratch.FindIndex(t => t.NetworkObject != null && t.NetworkObject.Id == _currentTarget.NetworkObject.Id);
      if (found >= 0) {
        idx = (found + 1) % _tabScratch.Count;
      }
    }

    SetTarget(_tabScratch[idx]);
  }

  // ---- LMB raycast ----

  void TrySelectFromScreenRay() {
    var cam = Camera.main;
    if (cam == null) {
      return;
    }

    var mouse = Mouse.current;
    if (mouse == null) {
      return;
    }

    var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    if (Physics.Raycast(ray, out var hitInfo, _maxRaycastDistance)) {
      var hit = hitInfo.collider.GetComponentInParent<Targetable>();
      if (hit != null) {
        SetTarget(hit);
      }
      // Click on empty geometry: keep current target (WoW classic behaviour).
    }
  }

  // ---- Target management ----

  void SetTarget(Targetable t) {
    if (_currentTarget == t) {
      return;
    }

    _currentTarget = t;
    string label = t != null ? t.DisplayName : "none";
    Debug.Log($"[TargetingController] Target -> '{label}'");

    if (TargetHighlight.Instance != null) {
      TargetHighlight.Instance.SetTarget(t);
    } else {
      Debug.LogWarning("[TargetingController] TargetHighlight.Instance is null — ring won't show.");
    }
  }

  void EnsureCamera() {
    if (_camera != null) {
      return;
    }
    _camera = Object.FindAnyObjectByType<ThirdPersonOrbitCamera>();
  }
}
