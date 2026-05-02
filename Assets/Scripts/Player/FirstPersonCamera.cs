using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pure render: follow the local player and apply mouse-driven yaw/pitch. Owns
/// the local view yaw, which is sampled by <see cref="KeyboardInputSource"/> and
/// shipped into <see cref="GameplayInput.LookYaw"/>. No knowledge of combat or
/// health state.
/// </summary>
public class FirstPersonCamera : MonoBehaviour {
  public Transform Target;
  public float MouseSensitivity = 10f;

  float _verticalRotation;
  float _horizontalRotation;

  /// <summary>Accumulated yaw from mouse deltas, in degrees.</summary>
  public float Yaw => _horizontalRotation;

  void LateUpdate() {
    if (Target == null) {
      TryFindLocalPlayerTarget();
      if (Target == null) {
        return;
      }
    }

    transform.position = Target.position;

    var mouse = Mouse.current;
    if (mouse == null) {
      return;
    }

    float scale = 0.05f * (MouseSensitivity / 10f);
    float mouseX = mouse.delta.x.ReadValue() * scale;
    float mouseY = mouse.delta.y.ReadValue() * scale;

    _verticalRotation -= mouseY * MouseSensitivity;
    _verticalRotation = Mathf.Clamp(_verticalRotation, -70f, 70f);
    _horizontalRotation += mouseX * MouseSensitivity;
    transform.rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
  }

  /// <summary>
  /// Finds the PlayerMovement that has input authority on this peer. Works
  /// correctly even when multiple NetworkRunner instances exist in the editor
  /// (multi-client simulation), because HasInputAuthority is scoped to each
  /// runner independently rather than relying on FindAnyObjectByType(Runner).
  /// </summary>
  void TryFindLocalPlayerTarget() {
    foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude)) {
      if (pm.HasInputAuthority) {
        Target = pm.transform;
        return;
      }
    }
  }
}
