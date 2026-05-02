using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Shared Mode Basics tutorial — simple first-person look on the main camera.
/// </summary>
public class FirstPersonCamera : MonoBehaviour {
  public Transform Target;
  public float MouseSensitivity = 10f;

  float _verticalRotation;
  float _horizontalRotation;

  void LateUpdate() {
    if (Target == null) {
      return;
    }

    transform.position = Target.position;

    if (Target.TryGetComponent(out Health hp) && hp.IsDead) {
      return;
    }

#if ENABLE_INPUT_SYSTEM
    var mouse = Mouse.current;
    if (mouse == null) {
      return;
    }

    float scale = 0.05f * (MouseSensitivity / 10f);
    float mouseX = mouse.delta.x.ReadValue() * scale;
    float mouseY = mouse.delta.y.ReadValue() * scale;
#else
    float mouseX = Input.GetAxis("Mouse X");
    float mouseY = Input.GetAxis("Mouse Y");
#endif

    _verticalRotation -= mouseY * MouseSensitivity;
    _verticalRotation = Mathf.Clamp(_verticalRotation, -70f, 70f);
    _horizontalRotation += mouseX * MouseSensitivity;
    transform.rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
  }
}
