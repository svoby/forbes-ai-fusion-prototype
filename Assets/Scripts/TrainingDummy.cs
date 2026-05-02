using UnityEngine;

/// <summary>
/// Visual marker for editor training targets (no gameplay logic).
/// </summary>
public class TrainingDummy : MonoBehaviour {
  void Awake() {
    if (TryGetComponent(out MeshRenderer r) && r.sharedMaterial != null) {
      r.material.color = new Color(0.9f, 0.35f, 0.15f, 1f);
    }
  }
}
