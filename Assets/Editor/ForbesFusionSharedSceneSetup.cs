#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene and prefab setup helpers for the Forbes Fusion prototype.
/// Run <b>Tools → Fusion → Scene → Apply Full Combat Setup</b> once after pulling
/// these scripts to wire everything automatically.
/// </summary>
public static class ForbesFusionSharedSceneSetup {
  const string FloorName               = "Floor";
  const string TargetHighlightName     = "TargetHighlight";
  const string PlayerPrefabPath        = "Assets/PlayerCharacter.prefab";
  const string TrainingDummyPrefabPath = "Assets/TrainingDummy.prefab";

  // -----------------------------------------------------------------------
  //  Full one-click setup — run this after pulling updated scripts
  // -----------------------------------------------------------------------

  [MenuItem("Tools/Fusion/Scene/Apply Full Combat Setup (run once)", false, 100)]
  public static void ApplyFullCombatSetup() {
    // Ensure networking objects exist in the scene first.
    FusionSceneSetupAssistants.AddNetworkingToScene();

    EnsureFloor();
    EnsureRunnerComponents();
    EnsureTargetHighlight();
    FixMainCamera();
    PatchPlayerPrefab();
    PatchTrainingDummyPrefab();

    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    EditorSceneManager.SaveOpenScenes();

    Debug.Log("Forbes: Full combat setup complete. Press Play to test.");
  }

  // -----------------------------------------------------------------------
  //  Legacy menu item — kept for reference
  // -----------------------------------------------------------------------

  [MenuItem("Tools/Fusion/Scene/Setup + Floor + Player Spawner (tutorial 2)", false, 105)]
  [MenuItem("GameObject/Fusion/Scene/Setup + Floor + Player Spawner (tutorial 2)", false, 105)]
  public static void SetupSharedModeScene() {
    FusionSceneSetupAssistants.AddNetworkingToScene();
    EnsureFloor();
    EnsureRunnerComponents();
    EnsureTargetHighlight();
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    EditorSceneManager.SaveOpenScenes();
    Debug.Log("Forbes: Shared mode scene setup complete.");
  }

  // -----------------------------------------------------------------------
  //  Step implementations
  // -----------------------------------------------------------------------

  static void EnsureFloor() {
    var existing = GameObject.Find(CheckerboardFloor.ParentName);

    // Replace a legacy single-plane Floor with the 3×3 checkerboard.
    if (existing != null && existing.transform.childCount == 0) {
      Undo.DestroyObjectImmediate(existing);
      existing = null;
    }

    if (existing != null) return; // already a checkerboard parent

    CheckerboardFloor.Create();
    var created = GameObject.Find(CheckerboardFloor.ParentName);
    if (created != null) Undo.RegisterCreatedObjectUndo(created, "Create Checkerboard Floor");
  }

  static void EnsureRunnerComponents() {
    var runner = Object.FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    if (runner == null) {
      Debug.LogError("ForbesFusionSharedSceneSetup: NetworkRunner not found. Run Fusion scene setup first.");
      return;
    }

    var go      = runner.gameObject;
    var spawner = EnsureComponent<PlayerSpawner>(go);
    EnsureComponent<KeyboardInputSource>(go);
    EnsureComponent<FusionInputProvider>(go);
    EnsureComponent<TargetingController>(go);
    var dummySpawner = EnsureComponent<TrainingDummySpawner>(go);
    EnsureComponent<CombatHud>(go);

    WireAssetRef(spawner, "PlayerPrefab", PlayerPrefabPath);
    WireAssetRef(dummySpawner, "TrainingDummyPrefab", TrainingDummyPrefabPath);
  }

  /// <summary>
  /// Replaces the deleted FirstPersonCamera (now a missing script) on the Main Camera
  /// with ThirdPersonOrbitCamera.
  /// </summary>
  static void FixMainCamera() {
    var cam = Camera.main;
    if (cam == null) {
      Debug.LogWarning("ForbesFusionSharedSceneSetup: Main Camera not found — add ThirdPersonOrbitCamera manually.");
      return;
    }

    var go = cam.gameObject;

    // Remove any MonoBehaviours whose script asset has been deleted.
    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
    if (removed > 0) {
      Debug.Log($"ForbesFusionSharedSceneSetup: Removed {removed} missing script(s) from Main Camera.");
      EditorUtility.SetDirty(go);
    }

    EnsureComponent<ThirdPersonOrbitCamera>(go);
    Debug.Log("ForbesFusionSharedSceneSetup: ThirdPersonOrbitCamera added to Main Camera.");
  }

  static void EnsureTargetHighlight() {
    if (GameObject.Find(TargetHighlightName) != null) {
      return;
    }

    var go = new GameObject(TargetHighlightName);
    go.AddComponent<LineRenderer>();
    go.AddComponent<TargetHighlight>();
    Undo.RegisterCreatedObjectUndo(go, "Create TargetHighlight");
    Debug.Log("ForbesFusionSharedSceneSetup: Created TargetHighlight ring.");
  }

  /// <summary>
  /// Patches PlayerCharacter.prefab: removes missing scripts, adds
  /// <see cref="NetworkCombatController"/> and <see cref="Targetable"/>.
  /// PlayerCombat (legacy stub) is left on the prefab for now to avoid
  /// breaking serialized references; it can be removed manually once
  /// NetworkCombatController is confirmed working.
  /// </summary>
  static void PatchPlayerPrefab() {
    var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
    if (contents == null) {
      Debug.LogError($"ForbesFusionSharedSceneSetup: Could not load prefab at {PlayerPrefabPath}");
      return;
    }

    bool dirty = false;

    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
    if (removed > 0) {
      Debug.Log($"ForbesFusionSharedSceneSetup: Removed {removed} missing script(s) from PlayerCharacter prefab.");
      dirty = true;
    }

    if (contents.GetComponent<NetworkCombatController>() == null) {
      contents.AddComponent<NetworkCombatController>();
      Debug.Log("ForbesFusionSharedSceneSetup: Added NetworkCombatController to PlayerCharacter.");
      dirty = true;
    }

    if (contents.GetComponent<Targetable>() == null) {
      var t = contents.AddComponent<Targetable>();
      // Set a sensible display name via SerializedObject since the field is private.
      var so = new SerializedObject(t);
      var prop = so.FindProperty("_displayName");
      if (prop != null) {
        prop.stringValue = "Player";
        so.ApplyModifiedPropertiesWithoutUndo();
      }

      Debug.Log("ForbesFusionSharedSceneSetup: Added Targetable to PlayerCharacter.");
      dirty = true;
    }

    if (dirty) {
      PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
    }

    PrefabUtility.UnloadPrefabContents(contents);
  }

  /// <summary>Patches TrainingDummy.prefab: adds <see cref="Targetable"/>.</summary>
  static void PatchTrainingDummyPrefab() {
    var contents = PrefabUtility.LoadPrefabContents(TrainingDummyPrefabPath);
    if (contents == null) {
      Debug.LogWarning($"ForbesFusionSharedSceneSetup: Could not load prefab at {TrainingDummyPrefabPath} — add Targetable manually.");
      return;
    }

    bool dirty = false;

    if (contents.GetComponent<Targetable>() == null) {
      var t = contents.AddComponent<Targetable>();
      var so = new SerializedObject(t);
      var prop = so.FindProperty("_displayName");
      if (prop != null) {
        prop.stringValue = "Training Dummy";
        so.ApplyModifiedPropertiesWithoutUndo();
      }

      Debug.Log("ForbesFusionSharedSceneSetup: Added Targetable to TrainingDummy.");
      dirty = true;
    }

    if (dirty) {
      PrefabUtility.SaveAsPrefabAsset(contents, TrainingDummyPrefabPath);
    }

    PrefabUtility.UnloadPrefabContents(contents);
  }

  // -----------------------------------------------------------------------
  //  Utilities
  // -----------------------------------------------------------------------

  static T EnsureComponent<T>(GameObject go) where T : Component {
    var c = go.GetComponent<T>();
    if (c == null) {
      c = Undo.AddComponent<T>(go);
    }

    return c;
  }

  static void WireAssetRef<T>(T component, string propertyName, string assetPath) where T : Object {
    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    if (asset == null) {
      Debug.LogWarning($"ForbesFusionSharedSceneSetup: Asset not found at {assetPath} — wire {propertyName} manually.");
      return;
    }

    var so   = new SerializedObject(component);
    var prop = so.FindProperty(propertyName);
    if (prop == null) {
      Debug.LogWarning($"ForbesFusionSharedSceneSetup: Property '{propertyName}' not found on {typeof(T).Name}.");
      return;
    }

    prop.objectReferenceValue = asset;
    so.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(component);
  }
}
#endif
