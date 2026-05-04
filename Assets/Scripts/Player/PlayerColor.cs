using Fusion;
using UnityEngine;

/// <summary>
/// Body colour via <see cref="Networked"/> property (default white); eyes stay a fixed tint.
/// Randomize from <see cref="GameplayInput"/> changes the body only.
/// </summary>
public class PlayerColor : NetworkBehaviour {
  static readonly Color DefaultBodyColor = Color.white;
  static readonly Color EyeTint        = new Color(0.12f, 0.45f, 0.95f, 1f);

  public MeshRenderer MeshRenderer;

  [Networked, OnChangedRender(nameof(ColorChanged))]
  public Color NetworkedColor { get; set; }

  NetworkButtons _prevButtons;
  MeshRenderer   _eyeLeft;
  MeshRenderer   _eyeRight;

  void Awake() {
    if (MeshRenderer == null) {
      MeshRenderer = GetComponent<MeshRenderer>();
    }

    var facing = transform.Find("FacingVisual");
    if (facing != null) {
      _eyeLeft  = facing.Find("Eye_L")?.GetComponent<MeshRenderer>();
      _eyeRight = facing.Find("Eye_R")?.GetComponent<MeshRenderer>();
    }
  }

  public override void Spawned() {
    if (HasStateAuthority && NetworkedColor.a <= 0f) {
      NetworkedColor = DefaultBodyColor;
    }

    RefreshVisuals();
  }

  void ColorChanged() {
    RefreshVisuals();
  }

  void RefreshVisuals() {
    if (MeshRenderer != null) {
      ApplyColor(MeshRenderer, NetworkedColor);
    }

    ApplyColor(_eyeLeft, EyeTint);
    ApplyColor(_eyeRight, EyeTint);
  }

  static void ApplyColor(MeshRenderer renderer, Color color) {
    if (renderer == null) {
      return;
    }

    var mat = renderer.material;
    mat.color = color;
    if (mat.HasProperty("_BaseColor")) {
      mat.SetColor("_BaseColor", color);
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
