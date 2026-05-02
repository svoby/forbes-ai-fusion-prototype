using Fusion;
using UnityEngine;

/// <summary>
/// Minimal local HUD: own HP and selected target HP (reads networked state only).
/// </summary>
public class CombatHud : MonoBehaviour {
  NetworkRunner _runner;
  string _line1 = "";
  string _line2 = "";

  void Awake() {
    _runner = GetComponent<NetworkRunner>();
  }

  void Update() {
    if (_runner == null || !_runner.IsRunning) {
      _line1 = "";
      _line2 = "";
      return;
    }

    if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj) || !playerObj.TryGetComponent(out Health self)) {
      _line1 = "HP: —";
      _line2 = "Cíl: —";
      return;
    }

    _line1 = $"HP: {self.NetworkedHealth:0}  (Tab=cíl, 1=kouzlo, E=barva)";
    if (playerObj.TryGetComponent(out PlayerCombat combat) && combat.TargetId.IsValid &&
        _runner.TryFindObject(combat.TargetId, out var targetObj) && targetObj.TryGetComponent(out Health targetHp)) {
      _line2 = $"Cíl HP: {targetHp.NetworkedHealth:0}";
    } else {
      _line2 = "Cíl: žádný";
    }
  }

  void OnGUI() {
    const float pad = 12f;
    var style = new GUIStyle(GUI.skin.label) {
      fontSize = 16,
      normal = { textColor = Color.white },
    };

    GUI.Label(new Rect(pad, pad, 900f, 28f), _line1, style);
    GUI.Label(new Rect(pad, pad + 22f, 900f, 28f), _line2, style);
  }
}
