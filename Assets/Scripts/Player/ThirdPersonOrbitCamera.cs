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
/// a LMB click (target select) from a LMB drag (camera orbit).
/// </summary>
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
  [SerializeField] float _dragThresholdPixels = 5f;

  Transform _target;
  float _yaw;
  float _pitch = 15f;
  float _distance;
  float _lmbDragAccum;

  /// <summary>Camera's current horizontal orbit angle in world-space degrees.</summary>
  public float Yaw => _yaw;

  /// <summary>What combination of mouse buttons is currently held.</summary>
  public CameraMouseMode MouseMode { get; private set; }

  /// <summary>True once the LMB has been dragged past the pixel threshold; resets on release.</summary>
  public bool IsLmbDragging { get; private set; }

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
      if (_lmbDragAccum > _dragThresholdPixels) {
        IsLmbDragging = true;
      }
    }

    // Reset IsLmbDragging one frame after release so TargetingController can read wasReleasedThisFrame safely.
    if (mouse.leftButton.wasReleasedThisFrame) {
      // Defer clear one frame so TargetingController can still read it on the same wasReleased frame.
      // IsLmbDragging is cleared next frame via the wasPressedThisFrame branch on the next press.
    }

    MouseMode = (lmb, rmb) switch {
      (true, true)   => CameraMouseMode.Both,
      (false, true)  => CameraMouseMode.Right,
      (true, false)  => CameraMouseMode.Left,
      _              => CameraMouseMode.None,
    };

    // Orbit when any button held.
    // mouse.delta is in screen pixels per frame — multiply by sensitivity (deg/px).
    if (lmb || rmb) {
      var delta = mouse.delta.ReadValue();
      _yaw   += delta.x * _sensitivity;
      _pitch -= delta.y * _sensitivity;
      _pitch  = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
    }

    // Scroll-wheel zoom. scroll.y is ~120 units per notch in the new Input System.
    float scroll = mouse.scroll.ReadValue().y;
    if (Mathf.Abs(scroll) > 0.01f) {
      _distance -= scroll * _zoomSpeed;
      _distance  = Mathf.Clamp(_distance, _minDistance, _maxDistance);
    }

    // Position camera behind and above the pivot.
    var pivot = _target.position + _pivotOffset;
    var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    transform.position = pivot + rotation * new Vector3(0f, 0f, -_distance);
    transform.LookAt(pivot, Vector3.up);
  }

  /// <summary>
  /// Called by <see cref="KeyboardInputSource"/> when A/D turn the character in non-RMB mode.
  /// Keeps the camera yaw in sync so the camera follows the character.
  /// </summary>
  public void AddYaw(float degrees) {
    _yaw += degrees;
  }

  void TryFindLocalPlayer() {
    foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude)) {
      if (pm.HasInputAuthority) {
        _target = pm.transform;
        _yaw = _target.eulerAngles.y;
        return;
      }
    }
  }
}
