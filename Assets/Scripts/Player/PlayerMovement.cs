using Fusion;
using UnityEngine;

/// <summary>
/// Tick-driven movement on State Authority. Reads <see cref="GameplayInput"/> and
/// applies planar movement + character facing without touching render-time camera state.
/// <para>
/// When <see cref="GameplayButtons.AlwaysFaceYaw"/> is set (RMB/Both mouse mode) the
/// character always faces <see cref="GameplayInput.LookYaw"/> regardless of move direction.
/// This matches WoW strafing: A/D strafe without the character turning sideways.
/// </para>
/// <para>
/// Movement is blocked while <see cref="NetworkCombatController.IsCasting"/> is true so
/// cast-time spells freeze the caster.
/// </para>
/// </summary>
public class PlayerMovement : NetworkBehaviour {
  CharacterController      _controller;
  Vector3                  _velocity;
  NetworkButtons           _prevButtons;
  Health                   _health;
  NetworkCombatController  _combat;   // may be null until added to prefab
  bool                     _loggedFirstInput;

  public float PlayerSpeed   = 40f;
  public float JumpForce     = 5f;
  public float GravityValue  = -9.81f;

  void Awake() {
    _controller = GetComponent<CharacterController>();
    _health     = GetComponent<Health>();
    _combat     = GetComponent<NetworkCombatController>();

    if (_controller == null) {
      Debug.LogError($"[PlayerMovement] CharacterController missing on '{name}' — movement will not work.", this);
    }

    if (GetComponent<Targetable>() == null) {
      gameObject.AddComponent<Targetable>();
    }

    if (GetComponent<FacingIndicator>() == null) {
      gameObject.AddComponent<FacingIndicator>();
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority || _controller == null) {
      return;
    }

    if (_health != null && _health.IsDead) {
      return;
    }

    // Freeze movement while casting a cast-time spell.
    if (_combat != null && _combat.IsCasting) {
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    // Log the first successful input tick so we can confirm input is flowing.
    if (!_loggedFirstInput) {
      _loggedFirstInput = true;
      Debug.Log($"[PlayerMovement] First input received: Move={input.Move} LookYaw={input.LookYaw:F1} obj={name}", this);
    }

    if (_controller.isGrounded) {
      _velocity = new Vector3(0f, -1f, 0f);
    }

    bool alwaysFace = input.Buttons.IsSet((int)GameplayButtons.AlwaysFaceYaw);

    Vector2 axes   = input.Move;
    Vector3 planar = new Vector3(axes.x, 0f, axes.y);
    Quaternion cameraYaw = Quaternion.Euler(0f, input.LookYaw, 0f);

    // Movement direction:
    //   AlwaysFaceYaw (RMB / Q-E / strafe) → move relative to camera yaw.
    //   LMB orbit only                     → move relative to character's own facing
    //                                         so the camera can orbit without pushing
    //                                         the character sideways.
    Quaternion moveYaw;
    if (alwaysFace) {
      moveYaw = cameraYaw;
    } else {
      var fwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
      moveYaw = fwd.sqrMagnitude > 1e-6f
        ? Quaternion.LookRotation(fwd.normalized)
        : cameraYaw;
    }

    Vector3 move = moveYaw * planar * (Runner.DeltaTime * PlayerSpeed);

    _velocity.y += GravityValue * Runner.DeltaTime;
    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Jump) && _controller.isGrounded) {
      _velocity.y += JumpForce;
    }

    _controller.Move(move + _velocity * Runner.DeltaTime);

    // Facing:
    //   AlwaysFaceYaw → always face camera yaw (RMB free-look, Q/E rotate, A/D strafe).
    //   LMB orbit     → NEVER change facing; camera orbits around stationary character.
    if (alwaysFace) {
      transform.forward = (cameraYaw * Vector3.forward).normalized;
    }

    _prevButtons = input.Buttons;
  }
}
