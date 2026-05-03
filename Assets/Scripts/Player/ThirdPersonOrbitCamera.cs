using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraMouseMode { None, Left, Right, Both }

// Ensures ThirdPersonOrbitCamera exists on Camera.main at runtime without requiring manual scene setup.
// Safe to call multiple times — skips if the component is already present.

/// <summary>
/// WoW-style third-person orbit camera.
/// <list type="bullet">
///   <item>No mouse held: camera stays. A/D turn the character (caller invokes <see cref="AddYaw"/>).</item>
///   <item>LMB held: camera orbits freely; character does NOT follow.</item>
///   <item>RMB held: camera and character rotate together (caller reads <see cref="Yaw"/> as LookYaw).</item>
///   <item>Both held: RMB behaviour + <see cref="KeyboardInputSource"/> forces auto-forward.</item>
/// </list>
/// Drag detection (<see cref="IsLmbDragging"/>) lets <see cref="TargetingController"/> distinguish
/// a LMB click (target select) from a LMB drag (camera orbit). Cursor hides during RMB held, or during LMB orbit once drag passes the pixel threshold (so clicks still raycast freely).
/// Execution order -50 guarantees LateUpdate runs before PlayerMovement (default 0) so
/// MouseMode and Yaw are always current when the character applies its facing.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ThirdPersonOrbitCamera : MonoBehaviour {
  /// <summary>Orbit speed in degrees per mouse-delta pixel.</summary>
  [SerializeField] float _sensitivity = 0.25f;
  [SerializeField] float _minPitch = -25f;
  [SerializeField] float _maxPitch = 70f;
  [SerializeField] float _minDistance = 1.5f;
  [SerializeField] float _maxDistance = 20f;
  [SerializeField] float _startDistance = 5f;
  /// <summary>Distance change per scroll notch (120 units per notch in the new Input System).</summary>
  [SerializeField] float _zoomSpeed = 0.03f;
  [SerializeField] Vector3 _pivotOffset = new Vector3(0f, 1.5f, 0f);
  [SerializeField] float _dragThresholdPixels = 20f;

  Transform _target;
  float _charYaw;       // character-facing yaw; Q/E and RMB drag modify this
  float _orbitOffset;   // extra camera angle added by LMB orbit; character ignores it
  float _pitch = 15f;
  float _distance;
  float _lmbDragAccum;
  float _rmbDragAccum;

  /// <summary>
  /// Character-facing yaw in world-space degrees.
  /// Q/E keyboard rotation and RMB drag modify this.
  /// LMB orbit does NOT affect this value, so the character never snaps
  /// to the camera orbit angle when Q/E are pressed mid-orbit.
  /// </summary>
  public float Yaw => _charYaw;

  /// <summary>What combination of mouse buttons is currently held.</summary>
  public CameraMouseMode MouseMode { get; private set; }

  /// <summary>True once the LMB has been dragged past the pixel threshold; resets on release.</summary>
  public bool IsLmbDragging { get; private set; }

  /// <summary>True once the RMB has moved past the drag threshold this press (mirrors LMB; cursor locks on RMB press regardless).</summary>
  bool IsRmbDragging { get; set; }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void AutoAddToMainCamera() {
    var cam = Camera.main;
    if (cam == null) {
      Debug.LogWarning("[ThirdPersonOrbitCamera] Camera.main not found at scene load — add ThirdPersonOrbitCamera manually.");
      return;
    }

    if (cam.GetComponent<ThirdPersonOrbitCamera>() != null) {
      return; // already configured (e.g. via scene setup tool)
    }

    cam.gameObject.AddComponent<ThirdPersonOrbitCamera>();
    Debug.Log("[ThirdPersonOrbitCamera] Auto-added to Main Camera. Run 'Tools/Fusion/Scene/Apply Full Combat Setup' to persist this.");
  }

  void Awake() {
    _distance = _startDistance;
  }

  void LateUpdate() {
    if (_target == null) {
      TryFindLocalPlayer();
      if (_target == null) {
        return;
      }
    }

    var mouse = Mouse.current;
    if (mouse == null) {
      return;
    }

    bool lmb = mouse.leftButton.isPressed;
    bool rmb = mouse.rightButton.isPressed;

    // Accumulate LMB drag pixels so TargetingController can tell click apart from drag.
    if (mouse.leftButton.wasPressedThisFrame) {
      _lmbDragAccum = 0f;
      IsLmbDragging = false;
    }
    if (lmb) {
      _lmbDragAccum += mouse.delta.ReadValue().magnitude;
      if (_lmbDragAccum > _dragThresholdPixels) IsLmbDragging = true;
    }
    // Reset on release (runs in LateUpdate, AFTER TargetingController.Update read the flag).
    if (mouse.leftButton.wasReleasedThisFrame) IsLmbDragging = false;

    if (mouse.rightButton.wasPressedThisFrame) {
      _rmbDragAccum = 0f;
      IsRmbDragging = false;
      // Snap character yaw to current camera angle so the player immediately
      // faces where the camera is pointing (WoW RMB behaviour).
      _charYaw    += _orbitOffset;
      _orbitOffset = 0f;
    }
    if (rmb) {
      _rmbDragAccum += mouse.delta.ReadValue().magnitude;
      if (_rmbDragAccum > _dragThresholdPixels) IsRmbDragging = true;
    }
    if (mouse.rightButton.wasReleasedThisFrame) IsRmbDragging = false;

    MouseMode = (lmb, rmb) switch {
      (true, true)   => CameraMouseMode.Both,
      (false, true)  => CameraMouseMode.Right,
      (true, false)  => CameraMouseMode.Left,
      _              => CameraMouseMode.None,
    };

    // Cursor: lock on RMB (free look). LMB keeps cursor until actual orbit drag crosses threshold — otherwise click-target raycasts still see the real mouse position.
    ManageCursor(rmb || (lmb && IsLmbDragging));

    // Mouse rotation — LMB and RMB do different things:
    //   LMB only  → orbit camera around character; _orbitOffset changes, _charYaw untouched.
    //   RMB only  → rotate character; _charYaw changes, camera follows.
    //   Both      → same as RMB (auto-forward + character rotation).
    if (lmb || rmb) {
      var delta = mouse.delta.ReadValue();
      _pitch -= delta.y * _sensitivity;
      _pitch  = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

      if (lmb && !rmb) {
        _orbitOffset += delta.x * _sensitivity;   // camera orbits, character stays
      } else {
        _charYaw     += delta.x * _sensitivity;   // character (and camera) rotate
      }
    }

    // Scroll-wheel zoom. scroll.y is ~120 units per notch in the new Input System.
    float scroll = mouse.scroll.ReadValue().y;
    if (Mathf.Abs(scroll) > 0.01f) {
      _distance -= scroll * _zoomSpeed;
      _distance  = Mathf.Clamp(_distance, _minDistance, _maxDistance);
    }

    // Position camera behind and above the pivot.
    // Total camera yaw = character yaw + LMB orbit offset.
    var pivot = _target.position + _pivotOffset;
    var rotation = Quaternion.Euler(_pitch, _charYaw + _orbitOffset, 0f);
    transform.position = pivot + rotation * new Vector3(0f, 0f, -_distance);
    transform.LookAt(pivot, Vector3.up);
  }

  static void ManageCursor(bool anyHeld) {
    if (anyHeld) {
      if (Cursor.lockState != CursorLockMode.Locked) {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
      }
    } else {
      if (Cursor.lockState != CursorLockMode.None) {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
      }
    }
  }

  /// <summary>
  /// Called by <see cref="KeyboardInputSource"/> when Q/E or arrow keys rotate the character.
  /// Modifies <see cref="_charYaw"/> only — the LMB orbit offset is unaffected, so pressing
  /// Q/E while orbiting with LMB turns the character without snapping to the orbit angle.
  /// </summary>
  public void AddYaw(float degrees) {
    _charYaw += degrees;
  }

  /// <summary>
  /// Called by <see cref="KeyboardInputSource"/> when Q/E are pressed while LMB is held.
  /// Rotates the character yaw but compensates <see cref="_orbitOffset"/> by the same amount
  /// so the camera stays at the same world angle — only the player turns.
  /// </summary>
  public void AddCharYawKeepCamera(float degrees) {
    _charYaw     += degrees;
    _orbitOffset -= degrees;
  }

  void TryFindLocalPlayer() {
    foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude)) {
      if (pm.HasInputAuthority) {
        _target      = pm.transform;
        _charYaw     = _target.eulerAngles.y;
        _orbitOffset = 0f;
        return;
      }
    }
  }
}
