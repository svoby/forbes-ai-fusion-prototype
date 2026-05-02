using Fusion;
using UnityEngine;

/// <summary>
/// Movement in <see cref="FixedUpdateNetwork"/> on State Authority; reads <see cref="GameplayInput"/> from Fusion input.
/// </summary>
public class PlayerMovement : NetworkBehaviour {
  CharacterController _controller;
  Vector3 _velocity;
  NetworkButtons _prevButtons;

  public float PlayerSpeed = 2f;
  public float JumpForce = 5f;
  public float GravityValue = -9.81f;
  public Camera ViewCamera;

  Health _health;

  void Awake() {
    _controller = GetComponent<CharacterController>();
    _health = GetComponent<Health>();
  }

  public override void Spawned() {
    if (!HasStateAuthority) {
      return;
    }

    ViewCamera = Camera.main;
    if (ViewCamera == null) {
      return;
    }

    var fp = ViewCamera.GetComponent<FirstPersonCamera>();
    if (fp != null) {
      fp.Target = transform;
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority || _controller == null) {
      return;
    }

    if (_health != null && _health.IsDead) {
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    if (_controller.isGrounded) {
      _velocity = new Vector3(0f, -1f, 0f);
    }

    Vector2 axes = input.Move;
    Vector3 planar = new Vector3(axes.x, 0f, axes.y);
    Vector3 move;
    if (ViewCamera != null) {
      var yaw = Quaternion.Euler(0f, ViewCamera.transform.eulerAngles.y, 0f);
      move = yaw * planar * (Runner.DeltaTime * PlayerSpeed);
    } else {
      move = planar * (Runner.DeltaTime * PlayerSpeed);
    }

    _velocity.y += GravityValue * Runner.DeltaTime;
    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Jump) && _controller.isGrounded) {
      _velocity.y += JumpForce;
    }

    _controller.Move(move + _velocity * Runner.DeltaTime);

    var faceDir = ViewCamera != null
      ? Quaternion.Euler(0f, ViewCamera.transform.eulerAngles.y, 0f) * planar
      : planar;
    if (faceDir.sqrMagnitude > 1e-6f) {
      transform.forward = faceDir.normalized;
    }

    _prevButtons = input.Buttons;
  }
}
