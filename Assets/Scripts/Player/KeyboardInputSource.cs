using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads keyboard and mouse state every frame and implements <see cref="IInputSource"/>
/// so <see cref="FusionInputProvider"/> can sample it from Fusion's OnInput.
/// <para>
/// Control scheme:
/// <list type="bullet">
///   <item>W / S / Up / Down — forward / back.</item>
///   <item>A / D            — lateral strafe (always, regardless of mouse mode).</item>
///   <item>Q / E / ← / →   — rotate camera (and character, since AlwaysFaceYaw = true).</item>
///   <item>LMB held         — orbit camera freely; A / D still strafe.</item>
///   <item>RMB held         — free-look; combined with strafe and keyboard rotation.</item>
///   <item>Both held        — auto-forward + free-look.</item>
/// </list>
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class KeyboardInputSource : MonoBehaviour, IInputSource {
  const float TurnRateDegPerSec = 60f;

  bool _pendingJump;
  bool _pendingSpell1;
  bool _pendingSpell2;
  bool _pendingSpell3;
  bool _pendingColor;

  ThirdPersonOrbitCamera _camera;

  // ---- IInputSource ----
  public Vector2 MoveAxes      { get; private set; }
  public float   LookYaw       { get; private set; }
  public bool    AlwaysFaceYaw { get; private set; }

  public bool ConsumeJump()           => Consume(ref _pendingJump);
  public bool ConsumeSpell1()         => Consume(ref _pendingSpell1);
  public bool ConsumeSpell2()         => Consume(ref _pendingSpell2);
  public bool ConsumeSpell3()         => Consume(ref _pendingSpell3);
  public bool ConsumeRandomizeColor() => Consume(ref _pendingColor);

  static bool Consume(ref bool flag) {
    if (!flag) return false;
    flag = false;
    return true;
  }

  int _missingCameraWarnFrame = -1;

  void EnsureCamera() {
    if (_camera != null) return;

    _camera = Object.FindAnyObjectByType<ThirdPersonOrbitCamera>();
    if (_camera != null) {
      LookYaw = _camera.Yaw;
      Debug.Log("[KeyboardInputSource] ThirdPersonOrbitCamera found.");
      return;
    }

    if (Time.frameCount != _missingCameraWarnFrame && Time.frameCount % 60 == 0) {
      _missingCameraWarnFrame = Time.frameCount;
      Debug.LogWarning("[KeyboardInputSource] ThirdPersonOrbitCamera not found — LookYaw=0. Run 'Tools/Fusion/Scene/Apply Full Combat Setup'.");
    }
  }

  void Update() {
    EnsureCamera();

    var kb = Keyboard.current;
    if (kb == null) return;

    // -- Edge presses (latched until consumed by FusionInputProvider) --
    if (kb.spaceKey.wasPressedThisFrame)  { _pendingJump   = true; }
    if (kb.digit1Key.wasPressedThisFrame) { _pendingSpell1 = true; }
    if (kb.digit2Key.wasPressedThisFrame) { _pendingSpell2 = true; }
    if (kb.digit3Key.wasPressedThisFrame) { _pendingSpell3 = true; }
    if (kb.rKey.wasPressedThisFrame)      { _pendingColor  = true; } // R = randomise colour

    // -- Auto-forward when both mouse buttons held --
    bool both = _camera != null && _camera.MouseMode == CameraMouseMode.Both;

    // -- Forward / back: W / S / Up / Down --
    bool wFwd  = kb.wKey.isPressed || kb.upArrowKey.isPressed;
    bool sBack = kb.sKey.isPressed || kb.downArrowKey.isPressed;
    float moveY = (wFwd ? 1f : 0f) - (sBack ? 1f : 0f);
    if (both) moveY = Mathf.Max(moveY, 1f);

    // -- Strafe: A / D always strafe regardless of mouse mode --
    float moveX = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);

    // Diagonal keyboard combos must not exceed unit length (classic √2 speed bug).
    var axes = new Vector2(moveX, moveY);
    if (axes.sqrMagnitude > 1f) {
      axes.Normalize();
    }

    MoveAxes = axes;

    // -- Rotate camera: Q / E and ← / → arrow keys --
    bool turnLeft  = kb.qKey.isPressed || kb.leftArrowKey.isPressed;
    bool turnRight = kb.eKey.isPressed || kb.rightArrowKey.isPressed;
    if (_camera != null) {
      bool lmbOnly = _camera.MouseMode == CameraMouseMode.Left;
      if (turnLeft) {
        if (lmbOnly) _camera.AddCharYawKeepCamera(-TurnRateDegPerSec * Time.deltaTime);
        else         _camera.AddYaw             (-TurnRateDegPerSec * Time.deltaTime);
      }
      if (turnRight) {
        if (lmbOnly) _camera.AddCharYawKeepCamera( TurnRateDegPerSec * Time.deltaTime);
        else         _camera.AddYaw             ( TurnRateDegPerSec * Time.deltaTime);
      }
    }

    // LookYaw mirrors the camera's current yaw.
    if (_camera != null) LookYaw = _camera.Yaw;

    // AlwaysFaceYaw rules:
    //   RMB held          → character locks to camera yaw (free-look rotates both)
    //   Q / E / ← / →    → keyboard rotation, character follows camera
    //   A / D strafe      → character must face forward while side-stepping
    //   LMB orbit only    → camera orbits freely; character keeps its own facing
    //   No input          → character keeps its own facing
    bool rmb = _camera != null &&
               (_camera.MouseMode == CameraMouseMode.Right ||
                _camera.MouseMode == CameraMouseMode.Both);
    AlwaysFaceYaw = rmb || turnLeft || turnRight || (moveX != 0f);
  }
}
