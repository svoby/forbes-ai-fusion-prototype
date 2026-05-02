using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads keyboard and mouse state every frame; implements <see cref="IInputSource"/>
/// so <see cref="FusionInputProvider"/> can sample it from Fusion's <c>OnInput</c>.
/// <para>
/// WoW mouse-mode rules:
/// <list type="bullet">
///   <item>No mouse: A/D turn the camera/character; character does not strafe.</item>
///   <item>LMB held: camera orbits; A/D still turn; <see cref="AlwaysFaceYaw"/> stays false.</item>
///   <item>RMB held: camera + character rotate together; A/D strafe; <see cref="AlwaysFaceYaw"/> = true.</item>
///   <item>Both held: same as RMB + <see cref="MoveAxes"/>.y forced to 1 (auto-forward).</item>
/// </list>
/// </para>
/// Edge button presses are latched and consumed once per Fusion tick via the Consume* methods.
/// </summary>
[DisallowMultipleComponent]
public class KeyboardInputSource : MonoBehaviour, IInputSource {
  const float TurnRateDegPerSec      = 120f;  // A / D keys
  const float ArrowTurnRateDegPerSec = 60f;   // ← → arrow keys (50 % slower)

  bool _pendingJump;
  bool _pendingSpell1;
  bool _pendingSpell2;
  bool _pendingSpell3;
  bool _pendingColor;

  ThirdPersonOrbitCamera _camera;

  // ---- IInputSource ----
  public Vector2 MoveAxes     { get; private set; }
  public float   LookYaw      { get; private set; }
  public bool    AlwaysFaceYaw { get; private set; }

  public bool ConsumeJump()           => Consume(ref _pendingJump);
  public bool ConsumeSpell1()         => Consume(ref _pendingSpell1);
  public bool ConsumeSpell2()         => Consume(ref _pendingSpell2);
  public bool ConsumeSpell3()         => Consume(ref _pendingSpell3);
  public bool ConsumeRandomizeColor() => Consume(ref _pendingColor);

  static bool Consume(ref bool flag) {
    if (!flag) {
      return false;
    }
    flag = false;
    return true;
  }

  int _missingCameraWarnFrame = -1;

  void EnsureCamera() {
    if (_camera != null) {
      return;
    }

    _camera = Object.FindAnyObjectByType<ThirdPersonOrbitCamera>();

    if (_camera != null) {
      LookYaw = _camera.Yaw;
      Debug.Log("[KeyboardInputSource] ThirdPersonOrbitCamera found.");
      return;
    }

    // Warn once per second (not every frame) so console stays readable.
    if (Time.frameCount != _missingCameraWarnFrame && Time.frameCount % 60 == 0) {
      _missingCameraWarnFrame = Time.frameCount;
      Debug.LogWarning("[KeyboardInputSource] ThirdPersonOrbitCamera not found — LookYaw=0, A/D turn disabled. Run 'Tools/Fusion/Scene/Apply Full Combat Setup'.");
    }
  }

  void Update() {
    EnsureCamera();

    var kb = Keyboard.current;
    if (kb == null) {
      return;
    }

    // -- Edge presses: latched until consumed by FusionInputProvider --
    if (kb.spaceKey.wasPressedThisFrame)  { _pendingJump   = true; }
    if (kb.digit1Key.wasPressedThisFrame) { _pendingSpell1 = true; }
    if (kb.digit2Key.wasPressedThisFrame) { _pendingSpell2 = true; }
    if (kb.digit3Key.wasPressedThisFrame) { _pendingSpell3 = true; }
    if (kb.eKey.wasPressedThisFrame)      { _pendingColor  = true; }

    // -- Mouse mode from camera --
    bool rmb  = _camera != null && (_camera.MouseMode == CameraMouseMode.Right || _camera.MouseMode == CameraMouseMode.Both);
    bool both = _camera != null && _camera.MouseMode == CameraMouseMode.Both;

    // -- Movement axes --
    bool wFwd      = kb.wKey.isPressed || kb.upArrowKey.isPressed;
    bool sBack     = kb.sKey.isPressed || kb.downArrowKey.isPressed;
    bool aKey      = kb.aKey.isPressed;
    bool leftArrow = kb.leftArrowKey.isPressed;
    bool dKey      = kb.dKey.isPressed;
    bool rightArrow = kb.rightArrowKey.isPressed;
    bool aLeft     = aKey || leftArrow;
    bool dRight    = dKey || rightArrow;

    float moveY = (wFwd ? 1f : 0f) - (sBack ? 1f : 0f);
    if (both) {
      moveY = Mathf.Max(moveY, 1f); // auto-forward when both buttons held
    }

    float moveX;
    if (rmb) {
      // RMB/Both: A/D + arrows = lateral strafe, character faces camera direction.
      moveX = (dRight ? 1f : 0f) - (aLeft ? 1f : 0f);
      AlwaysFaceYaw = true;
    } else {
      // No mouse / LMB only: A/D + arrows = turn camera (character follows camera yaw).
      // Arrow keys turn at half the rate of A/D for finer control.
      moveX = 0f;
      AlwaysFaceYaw = false;
      if (_camera != null) {
        if (aLeft) {
          float rate = (!aKey && leftArrow) ? ArrowTurnRateDegPerSec : TurnRateDegPerSec;
          _camera.AddYaw(-rate * Time.deltaTime);
        }
        if (dRight) {
          float rate = (!dKey && rightArrow) ? ArrowTurnRateDegPerSec : TurnRateDegPerSec;
          _camera.AddYaw(rate * Time.deltaTime);
        }
      }
    }

    MoveAxes = new Vector2(moveX, moveY);

    // LookYaw always mirrors the camera's current yaw.
    if (_camera != null) {
      LookYaw = _camera.Yaw;
    }
  }
}
