using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attached to the NetworkRunner GameObject by the scene setup tool.
/// Keeps the Fusion bootstrap debug HUD visible until the local player has joined
/// (<see cref="NetworkRunner.TryGetPlayerObject"/> for <see cref="NetworkRunner.LocalPlayer"/>),
/// then auto-hides so the play view is clean. F1 toggles the HUD any time afterward.
///
/// The bootstrap <see cref="FusionBootstrapDebugGUI"/> must stay enabled at least until Host/Client
/// can be used from OnGUI. Scenes sometimes serialize it disabled; <see cref="Awake"/> forces it on.
/// </summary>
[DisallowMultipleComponent]
public class FusionHudToggle : MonoBehaviour {
  FusionBootstrapDebugGUI _hud;
  NetworkRunner            _runner;
  bool                     _autoHidden;

  void Awake() {
    // Scene (or prior session) may serialize FusionBootstrapDebugGUI disabled — without OnGUI,
    // Host/Client never appear. Force-enable until we intentionally auto-hide after join.
    if (_runner == null) _runner = GetComponent<NetworkRunner>();
    if (_hud == null) _hud = Object.FindAnyObjectByType<FusionBootstrapDebugGUI>(FindObjectsInactive.Include);
    if (_hud != null) {
      _hud.enabled = true;
    }
  }

  void Update() {
    if (_runner == null) _runner = GetComponent<NetworkRunner>();
    if (_hud == null)    _hud    = Object.FindAnyObjectByType<FusionBootstrapDebugGUI>(FindObjectsInactive.Include);
    if (_hud == null)    return;

    // Hide only after we're in-session *and* the local player object exists (post-join / post-spawn).
    if (!_autoHidden
        && _runner != null
        && _runner.IsRunning
        && _runner.TryGetPlayerObject(_runner.LocalPlayer, out var localObj)
        && localObj != null) {
      _hud.enabled = false;
      _autoHidden  = true;
    }

    if (Keyboard.current?.f1Key.wasPressedThisFrame == true) {
      _hud.enabled = !_hud.enabled;
    }
  }
}
