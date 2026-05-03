using UnityEngine;

/// <summary>
/// Visual marker for editor training targets (no gameplay logic).
/// </summary>
public class TrainingDummy : MonoBehaviour {
  void Awake() {
    if (TryGetComponent(out MeshRenderer r) && r.sharedMaterial != null) {
      r.material.color = new Color(0.9f, 0.35f, 0.15f, 1f);
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
}
