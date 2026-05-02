using UnityEngine;

/// <summary>
/// Two small spheres ("eyes") on the front face of the character capsule so the
/// facing direction is immediately obvious.  Shared by PlayerCharacter and
/// TrainingDummy — attach to the prefab root or let Awake add it automatically.
/// </summary>
[DisallowMultipleComponent]
public class FacingIndicator : MonoBehaviour {
  // Bright teal — visible against any background, not confused with health/damage colours.
  static readonly Color EyeColor = new Color(0.05f, 0.85f, 0.95f);

  // Standard Unity capsule: height 2, radius 0.5, bottom at y=0.
  // Eyes sit in the upper hemisphere, forward face.
  const float EyeY        = 1.68f;   // vertical position on the capsule face
  const float EyeZ        = 0.42f;   // forward offset (≈ capsule surface at that height)
  const float EyeSpread   = 0.13f;   // half the horizontal distance between the two eyes
  const float EyeRadius   = 0.07f;   // radius of each sphere

  void Start() {
    var mat = BuildMaterial();
    CreateEye("Eye_L", new Vector3(-EyeSpread, EyeY, EyeZ), mat);
    CreateEye("Eye_R", new Vector3( EyeSpread, EyeY, EyeZ), mat);
  }

  void CreateEye(string eyeName, Vector3 localPos, Material mat) {
    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = eyeName;
    go.transform.SetParent(transform, false);
    go.transform.localPosition = localPos;
    go.transform.localScale    = Vector3.one * (EyeRadius * 2f);
    Destroy(go.GetComponent<Collider>()); // never interfere with targeting raycast
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
  }

  static Material BuildMaterial() {
    var shader = Shader.Find("Universal Render Pipeline/Lit")
              ?? Shader.Find("Standard")
              ?? Shader.Find("Sprites/Default");
    var mat = new Material(shader) { color = EyeColor };
    // Subtle self-illumination so the eyes read well in shadow.
    mat.EnableKeyword("_EMISSION");
    mat.SetColor("_EmissionColor", EyeColor * 0.4f);
    return mat;
  }
}
