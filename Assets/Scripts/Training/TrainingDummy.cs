using UnityEngine;

/// <summary>
/// Visual marker for editor training targets (no gameplay logic).
/// </summary>
public class TrainingDummy : MonoBehaviour {
  static readonly Color BodyTint = new Color(0.9f, 0.14f, 0.12f, 1f);

  void Awake() {
    if (TryGetComponent(out MeshRenderer body) && body.sharedMaterial != null) {
      ApplyColor(body, BodyTint);
    }

    var facing = transform.Find("FacingVisual");
    if (facing != null) {
      foreach (var eyeName in new[] { "Eye_L", "Eye_R" }) {
        var t = facing.Find(eyeName);
        if (t != null && t.TryGetComponent<MeshRenderer>(out var eye)) {
          ApplyColor(eye, Color.white);
        }
      }
    }

    // Ensure a physics collider exists so click-targeting raycasts can hit this object.
    if (GetComponentInChildren<Collider>() == null) {
      var col = gameObject.AddComponent<CapsuleCollider>();
      col.height = 2f;
      col.radius = 0.4f;
      col.center = new Vector3(0f, 1f, 0f);
      Debug.Log("[TrainingDummy] Added CapsuleCollider for raycast targeting.", this);
    }

    if (GetComponent<Targetable>() == null) {
      gameObject.AddComponent<Targetable>();
    }
  }

  static void ApplyColor(MeshRenderer renderer, Color color) {
    var mat = renderer.material;
    mat.color = color;
    if (mat.HasProperty("_BaseColor")) {
      mat.SetColor("_BaseColor", color);
    }
  }
}
