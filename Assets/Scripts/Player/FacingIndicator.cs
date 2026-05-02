using UnityEngine;

/// <summary>
/// Spawns a small green arrow in front of the character so the facing direction is
/// immediately obvious during development.  Two primitives (shaft + tip) form a
/// forward-pointing arrow at head height.
///
/// Colliders are removed so the indicator never interferes with raycasts or physics.
/// </summary>
[DisallowMultipleComponent]
public class FacingIndicator : MonoBehaviour {
  [SerializeField] Color _color        = new Color(0.1f, 1f, 0.25f);
  [SerializeField] float _headHeight   = 1.1f;
  [SerializeField] float _shaftLength  = 0.28f;
  [SerializeField] float _shaftRadius  = 0.04f;
  [SerializeField] float _tipRadius    = 0.1f;

  void Start() {
    BuildArrow();
  }

  void BuildArrow() {
    var mat = BuildMaterial();

    // Shaft — a capsule rotated so its long axis aligns with +Z (forward).
    var shaft = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    shaft.name = "FacingShaft";
    shaft.transform.SetParent(transform, false);
    shaft.transform.localPosition = new Vector3(0f, _headHeight, _shaftLength * 0.5f);
    shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    shaft.transform.localScale    = new Vector3(_shaftRadius * 2f, _shaftLength * 0.5f, _shaftRadius * 2f);
    Destroy(shaft.GetComponent<Collider>());
    shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;

    // Tip — a sphere at the front of the shaft.
    var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    tip.name = "FacingTip";
    tip.transform.SetParent(transform, false);
    tip.transform.localPosition = new Vector3(0f, _headHeight, _shaftLength + _tipRadius);
    tip.transform.localScale    = Vector3.one * (_tipRadius * 2f);
    Destroy(tip.GetComponent<Collider>());
    tip.GetComponent<MeshRenderer>().sharedMaterial = mat;
  }

  Material BuildMaterial() {
    // Try to use URP Lit; fall back to any standard shader that accepts color.
    var shader = Shader.Find("Universal Render Pipeline/Lit")
              ?? Shader.Find("Standard")
              ?? Shader.Find("Sprites/Default");
    var mat = new Material(shader) { color = _color };
    mat.EnableKeyword("_EMISSION");
    mat.SetColor("_EmissionColor", _color * 0.6f);
    return mat;
  }
}
