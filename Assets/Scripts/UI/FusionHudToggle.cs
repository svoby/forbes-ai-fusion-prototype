using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attached to the NetworkRunner GameObject by the scene setup tool.
/// Auto-hides the Fusion bootstrap debug HUD once the network session is running,
/// then lets the user toggle it with F1.
///
/// The HUD must remain enabled at startup so FusionBootstrapDebugGUI.OnGUI can
/// call StartMultipleSharedClients / StartGame. We hide it only after the runner
/// reports IsRunning = true.
/// </summary>
[DisallowMultipleComponent]
public class FusionHudToggle : MonoBehaviour {
  FusionBootstrapDebugGUI _hud;
  NetworkRunner            _runner;
  bool                     _autoHidden;

  void Update() {
    if (_runner == null) _runner = GetComponent<NetworkRunner>();
    if (_hud == null)    _hud    = Object.FindAnyObjectByType<FusionBootstrapDebugGUI>(FindObjectsInactive.Include);
    if (_hud == null)    return;

    // Hide once the session is live (lets OnGUI trigger StartGame first).
    if (!_autoHidden && _runner != null && _runner.IsRunning) {
      _hud.enabled = false;
      _autoHidden  = true;
    }

    if (Keyboard.current?.f1Key.wasPressedThisFrame == true) {
      _hud.enabled = !_hud.enabled;
    }
  }
}
