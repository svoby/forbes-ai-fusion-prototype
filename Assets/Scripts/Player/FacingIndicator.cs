using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Two "eye" spheres on the front face of the character capsule.
/// Position and size are derived from the <see cref="CharacterController"/>
/// dimensions at runtime, so they adapt to any capsule height / radius.
/// Falls back to standard 2 m capsule defaults when no CharacterController
/// is present (e.g. Training Dummy).
/// Subscribes to <see cref="Health.IsDeadChanged"/> and hides/shows the eyes
/// on death/respawn so they don't float after the body is hidden.
/// </summary>
[DisallowMultipleComponent]
public class FacingIndicator : MonoBehaviour {
  static readonly Color EyeColor = new Color(0.05f, 0.85f, 0.95f);

  readonly List<GameObject> _eyes = new List<GameObject>();
  Health _health;

  void OnEnable() {
    _health = GetComponent<Health>();
    if (_health != null) _health.IsDeadChanged += OnDeadChanged;
  }

  void OnDisable() {
    if (_health != null) _health.IsDeadChanged -= OnDeadChanged;
  }

  void OnDeadChanged(bool isDead) {
    foreach (var eye in _eyes) {
      if (eye != null) eye.SetActive(!isDead);
    }
  }

  void Start() {
    // ---- Read capsule geometry ----
    float height, radius, centerY;
    var cc = GetComponent<CharacterController>();
    if (cc != null) {
      height  = cc.height;
      radius  = cc.radius;
      centerY = cc.center.y;
    } else {
      height  = 2f;   // standard Unity capsule
      radius  = 0.5f;
      centerY = 1f;
    }

    float bottomY   = centerY - height * 0.5f;
    float topHemiCY = centerY + height * 0.5f - radius; // top hemisphere centre

    // Eyes at 82 % of total height from bottom → upper face area.
    float eyeY = bottomY + height * 0.82f;

    // Forward Z = capsule surface at that height.
    // Inside top hemisphere: z = sqrt(r² − dy²); inside cylinder: z = r.
    float dy = eyeY - topHemiCY;
    float eyeZ = (dy > 0f && dy < radius)
      ? Mathf.Sqrt(radius * radius - dy * dy)
      : radius;
    eyeZ *= 0.90f; // pull slightly inside so sphere doesn't clip the surface

    float spread  = radius * 0.30f;  // half the horizontal gap between eyes
    float eyeSize = radius * 0.26f;  // sphere radius — "a bit bigger"

    var mat = BuildMaterial();
    CreateEye("Eye_L", new Vector3(-spread, eyeY, eyeZ), eyeSize, mat);
    CreateEye("Eye_R", new Vector3( spread, eyeY, eyeZ), eyeSize, mat);
  }

  void CreateEye(string eyeName, Vector3 localPos, float eyeRadius, Material mat) {
    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    eye.name = eyeName;
    eye.transform.SetParent(transform, false);
    eye.transform.localPosition = localPos;
    eye.transform.localScale    = Vector3.one * (eyeRadius * 2f);
    Destroy(eye.GetComponent<Collider>());
    eye.GetComponent<MeshRenderer>().sharedMaterial = mat;
    _eyes.Add(eye);
  }

  static Material BuildMaterial() {
    var shader = Shader.Find("Universal Render Pipeline/Lit")
              ?? Shader.Find("Standard")
              ?? Shader.Find("Sprites/Default");
    var mat = new Material(shader) { color = EyeColor };
    mat.EnableKeyword("_EMISSION");
    mat.SetColor("_EmissionColor", EyeColor * 0.4f);
    return mat;
  }
}
