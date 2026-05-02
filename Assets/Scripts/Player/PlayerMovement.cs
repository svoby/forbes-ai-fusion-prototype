using Fusion;
using UnityEngine;

/// <summary>
/// Tick-driven movement on State Authority. Reads <see cref="GameplayInput"/>
/// (including <see cref="GameplayInput.LookYaw"/>) and applies planar movement +
/// facing without sampling render-time camera state.
/// </summary>
public class PlayerMovement : NetworkBehaviour {
  CharacterController _controller;
  Vector3 _velocity;
  NetworkButtons _prevButtons;

  public float PlayerSpeed = 2f;
  public float JumpForce = 5f;
  public float GravityValue = -9.81f;

  Health _health;

  void Awake() {
    _controller = GetComponent<CharacterController>();
    _health = GetComponent<Health>();
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
    Quaternion yaw = Quaternion.Euler(0f, input.LookYaw, 0f);
    Vector3 move = yaw * planar * (Runner.DeltaTime * PlayerSpeed);

    _velocity.y += GravityValue * Runner.DeltaTime;
    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Jump) && _controller.isGrounded) {
      _velocity.y += JumpForce;
    }

    _controller.Move(move + _velocity * Runner.DeltaTime);

    Vector3 faceDir = yaw * planar;
    if (faceDir.sqrMagnitude > 1e-6f) {
      transform.forward = faceDir.normalized;
    }

    _prevButtons = input.Buttons;
  }
}
