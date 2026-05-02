using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads keyboard movement axes every frame and latches edge presses (Space, Tab,
/// Digit1, E) so that Fusion's <c>OnInput</c> can sample them once per tick without
/// missing inputs that fall between ticks. Look yaw is sourced from the local
/// <see cref="FirstPersonCamera"/> (mouse → camera yaw is the player's look intent).
/// </summary>
[DisallowMultipleComponent]
public class KeyboardInputSource : MonoBehaviour, IInputSource {
  bool _pendingJump;
  bool _pendingTab;
  bool _pendingSpell;
  bool _pendingColor;

  FirstPersonCamera _camera;

  public Vector2 MoveAxes {
    get {
      var kb = Keyboard.current;
      if (kb == null) {
        return Vector2.zero;
      }

      float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
      float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
      return new Vector2(x, y);
    }
  }

  public float LookYaw {
    get {
      EnsureCamera();
      return _camera != null ? _camera.Yaw : 0f;
    }
  }

  public bool ConsumeJump() => Consume(ref _pendingJump);
  public bool ConsumeTabTarget() => Consume(ref _pendingTab);
  public bool ConsumeSpellPrimary() => Consume(ref _pendingSpell);
  public bool ConsumeRandomizeColor() => Consume(ref _pendingColor);

  static bool Consume(ref bool flag) {
    if (!flag) {
      return false;
    }

    flag = false;
    return true;
  }

  void EnsureCamera() {
    if (_camera != null) {
      return;
    }

    var cam = Camera.main;
    if (cam != null) {
      _camera = cam.GetComponent<FirstPersonCamera>();
    }
  }

  void Update() {
    var kb = Keyboard.current;
    if (kb == null) {
      return;
    }

    if (kb.spaceKey.wasPressedThisFrame) {
      _pendingJump = true;
    }

    if (kb.tabKey.wasPressedThisFrame) {
      _pendingTab = true;
    }

    if (kb.digit1Key.wasPressedThisFrame) {
      _pendingSpell = true;
    }

    if (kb.eKey.wasPressedThisFrame) {
      _pendingColor = true;
    }
  }
}
