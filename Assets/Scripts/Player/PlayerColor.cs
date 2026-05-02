using Fusion;
using UnityEngine;

/// <summary>
/// Random player colour via <see cref="Networked"/> property; intent from <see cref="GameplayInput"/>.
/// </summary>
public class PlayerColor : NetworkBehaviour {
  public MeshRenderer MeshRenderer;

  [Networked, OnChangedRender(nameof(ColorChanged))]
  public Color NetworkedColor { get; set; }

  NetworkButtons _prevButtons;

  void Awake() {
    if (MeshRenderer == null) {
      MeshRenderer = GetComponent<MeshRenderer>();
    }
  }

  void ColorChanged() {
    if (MeshRenderer != null) {
      MeshRenderer.material.color = NetworkedColor;
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.RandomizeColor)) {
      NetworkedColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
    }

    _prevButtons = input.Buttons;
  }
}
