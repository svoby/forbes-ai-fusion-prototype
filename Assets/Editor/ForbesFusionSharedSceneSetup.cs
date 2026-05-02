#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies the same scene objects as <c>GameObject/Fusion/Scene/Setup Networking in the Scene</c>,
/// adds a floor, wires <see cref="PlayerSpawner"/> on the runner, and saves the open scene.
/// </summary>
public static class ForbesFusionSharedSceneSetup {
  const string FloorName = "Floor";
  const string PlayerPrefabPath = "Assets/PlayerCharacter.prefab";
  const string TrainingDummyPrefabPath = "Assets/TrainingDummy.prefab";

  /// <summary>Listed under Tools/Fusion so it appears next to Photon's own Fusion scene tools.</summary>
  [MenuItem("Tools/Fusion/Scene/Setup + Floor + Player Spawner (tutorial 2)", false, 105)]
  [MenuItem("GameObject/Fusion/Scene/Setup + Floor + Player Spawner (tutorial 2)", false, 105)]
  public static void SetupSharedModeScene() {
    FusionSceneSetupAssistants.AddNetworkingToScene();
    EnsureFloor();
    EnsurePlayerSpawner();
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    EditorSceneManager.SaveOpenScenes();
    Debug.Log("Forbes: Shared mode scene setup complete (Fusion networking + Floor + PlayerSpawner).");
  }

  static void EnsureFloor() {
    if (GameObject.Find(FloorName) != null) {
      return;
    }

    var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
    plane.name = FloorName;
    plane.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    Undo.RegisterCreatedObjectUndo(plane, "Create Floor");
  }

  static void EnsurePlayerSpawner() {
    var runner = Object.FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    if (runner == null) {
      Debug.LogError("ForbesFusionSharedSceneSetup: NetworkRunner not found after Fusion setup.");
      return;
    }

    var spawner = runner.GetComponent<PlayerSpawner>();
    if (spawner == null) {
      spawner = Undo.AddComponent<PlayerSpawner>(runner.gameObject);
    }

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
    if (prefab == null) {
      Debug.LogError($"ForbesFusionSharedSceneSetup: Missing prefab at {PlayerPrefabPath}");
      return;
    }

    var so = new SerializedObject(spawner);
    so.FindProperty("PlayerPrefab").objectReferenceValue = prefab;
    var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummyPrefabPath);
    if (dummyPrefab != null) {
      so.FindProperty("TrainingDummyPrefab").objectReferenceValue = dummyPrefab;
    } else {
      Debug.LogWarning($"ForbesFusionSharedSceneSetup: Missing training dummy at {TrainingDummyPrefabPath}");
    }

    so.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(spawner);
  }
}
#endif
