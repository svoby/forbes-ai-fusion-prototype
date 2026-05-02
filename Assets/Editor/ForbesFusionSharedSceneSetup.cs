#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies the same scene objects as <c>GameObject/Fusion/Scene/Setup Networking in the Scene</c>,
/// adds a floor, wires <see cref="PlayerSpawner"/>, <see cref="FusionInputProvider"/>,
/// <see cref="KeyboardInputSource"/>, <see cref="TrainingDummySpawner"/> and the local
/// <see cref="CombatHud"/> on the runner, then saves the open scene.
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
    EnsureRunnerComponents();
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    EditorSceneManager.SaveOpenScenes();
    Debug.Log("Forbes: Shared mode scene setup complete (Fusion networking + Floor + spawner/input/HUD).");
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

  static void EnsureRunnerComponents() {
    var runner = Object.FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    if (runner == null) {
      Debug.LogError("ForbesFusionSharedSceneSetup: NetworkRunner not found after Fusion setup.");
      return;
    }

    var go = runner.gameObject;
    var spawner = EnsureComponent<PlayerSpawner>(go);
    EnsureComponent<KeyboardInputSource>(go);
    EnsureComponent<FusionInputProvider>(go);
    var dummySpawner = EnsureComponent<TrainingDummySpawner>(go);
    EnsureComponent<CombatHud>(go);

    var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
    if (playerPrefab == null) {
      Debug.LogError($"ForbesFusionSharedSceneSetup: Missing prefab at {PlayerPrefabPath}");
    } else {
      var so = new SerializedObject(spawner);
      so.FindProperty("PlayerPrefab").objectReferenceValue = playerPrefab;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(spawner);
    }

    var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummyPrefabPath);
    if (dummyPrefab == null) {
      Debug.LogWarning($"ForbesFusionSharedSceneSetup: Missing training dummy at {TrainingDummyPrefabPath}");
    } else {
      var so = new SerializedObject(dummySpawner);
      so.FindProperty("TrainingDummyPrefab").objectReferenceValue = dummyPrefab;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(dummySpawner);
    }
  }

  static T EnsureComponent<T>(GameObject go) where T : Component {
    var c = go.GetComponent<T>();
    if (c == null) {
      c = Undo.AddComponent<T>(go);
    }

    return c;
  }
}
#endif
