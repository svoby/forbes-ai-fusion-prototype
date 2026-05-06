#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

  [MenuItem("Tools/Fusion/Scene/Ensure Cast Bar HUD Only", false, 101)]
  public static void MenuEnsureCastBarHudOnly() {
    if (Application.isPlaying) {
      return;
    }

    int created = EnsureCastBarHudForLoadedScenes();
    if (created > 0) {
      Debug.Log($"Forbes: ForbesHudCanvas added under NetworkRunner ({created}). Save the scene.");
    }
  }

  /// <summary>Places cast bar under runners in scenes that are loaded in the editor. Does not run automatically.</summary>
  static int EnsureCastBarHudForLoadedScenes() {
    int created = 0;

    foreach (NetworkRunner runner in Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include)) {
      if (runner == null || !runner.gameObject.scene.IsValid()) {
        continue;
      }

      if (PrefabUtility.IsPartOfPrefabAsset(runner.gameObject)) {
        continue;
      }

      if (FindHudCanvasDirectChild(runner) != null) {
        continue;
      }

      EnsureHudCanvas(runner);
      created++;

      EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
    }

    return created;
  }

  [MenuItem("Tools/Fusion/Scene/Apply Full Combat Setup (run once)", false, 100)]
  public static void ApplyFullCombatSetup() {
    // Ensure networking objects exist in the scene first.
    FusionSceneSetupAssistants.AddNetworkingToScene();
    EnsureFusionBootstrapMenuVisible();

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
    EnsureFusionBootstrapMenuVisible();
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

    // Replace legacy flat Floor or an older saved grid (e.g. 3×3) with the current checkerboard.
    if (existing != null) {
      if (CheckerboardFloor.MatchesCurrentGrid(existing)) return;
      Undo.DestroyObjectImmediate(existing);
      existing = null;
    }

    CheckerboardFloor.Create();
    var created = GameObject.Find(CheckerboardFloor.ParentName);
    if (created != null) Undo.RegisterCreatedObjectUndo(created, "Create Checkerboard Floor");
  }

  /// <summary>
  /// Fusion sometimes saves <see cref="FusionBootstrap"/> / <see cref="FusionBootstrapDebugGUI"/> disabled;
  /// then Host/Client IMGUI never runs until something re-enables the component (e.g. F1).
  /// </summary>
  static void EnsureFusionBootstrapMenuVisible() {
    foreach (var bootstrap in Object.FindObjectsByType<FusionBootstrap>(FindObjectsInactive.Include)) {
      if (bootstrap == null) continue;
      Undo.RecordObject(bootstrap, "Enable FusionBootstrap");
      bootstrap.enabled = true;
      var gui = bootstrap.GetComponent<FusionBootstrapDebugGUI>();
      if (gui != null) {
        Undo.RecordObject(gui, "Enable FusionBootstrapDebugGUI");
        gui.enabled = true;
      }
    }
  }

  static void EnsureRunnerComponents() {
    var runner = Object.FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    if (runner == null) {
      Debug.LogError("ForbesFusionSharedSceneSetup: NetworkRunner not found. Run Fusion scene setup first.");
      return;
    }

    var go      = runner.gameObject;
    var spawner = ForbesEditorObjectUtility.EnsureComponent<PlayerSpawner>(go);
    ForbesEditorObjectUtility.EnsureComponent<KeyboardInputSource>(go);
    ForbesEditorObjectUtility.EnsureComponent<FusionInputProvider>(go);
    ForbesEditorObjectUtility.EnsureComponent<TargetingController>(go);
    ForbesEditorObjectUtility.EnsureComponent<SelectedTargetHealthBar>(go);
    var dummySpawner = ForbesEditorObjectUtility.EnsureComponent<TrainingDummySpawner>(go);
    ForbesEditorObjectUtility.EnsureComponent<FusionHudToggle>(go);
    EnsureHudCanvas(runner);

    ForbesEditorObjectUtility.WireAssetRef(spawner, "PlayerPrefab", PlayerPrefabPath);
    ForbesEditorObjectUtility.WireAssetRef(dummySpawner, "TrainingDummyPrefab", TrainingDummyPrefabPath);
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

    ForbesEditorObjectUtility.EnsureComponent<ThirdPersonOrbitCamera>(go);
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
  //  HUD canvas (cast bar)
  // -----------------------------------------------------------------------

  static Transform FindHudCanvasDirectChild(NetworkRunner runner) {
    Transform t = runner.transform;
    for (var i = 0; i < t.childCount; i++) {
      Transform c = t.GetChild(i);
      if (c != null && c.name == RuntimeHudBootstrap.HudCanvasChildName) {
        return c;
      }
    }

    return null;
  }

  /// <summary>
  /// One overlay canvas under the runner with the runtime HUD root and player-facing
  /// HUD views wired. Destroys a stale canvas and rebuilds.
  /// Does not create an EventSystem (not needed for passive rendering).
  /// </summary>
  static void EnsureHudCanvas(NetworkRunner runner) {
    var existing = FindHudCanvasDirectChild(runner);
    if (existing != null) {
      var existingHud = existing.GetComponent<RuntimeHudBootstrap>();
      if (existingHud != null && existingHud.UsesCurrentHudLayout) {
        return;
      }
      // Stale layout — destroy so we can rebuild with the banner panel.
      Undo.DestroyObjectImmediate(existing.gameObject);
    }

    Font uiFont = CastBarView.ResolveDefaultHudFont(26);

    var white = Sprite.Create(
      Texture2D.whiteTexture,
      new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
      new Vector2(0.5f, 0.5f),
      100f);

    GameObject canvasGo = new GameObject(RuntimeHudBootstrap.HudCanvasChildName);
    Undo.RegisterCreatedObjectUndo(canvasGo, "Forbes HUD Canvas");
    canvasGo.transform.SetParent(runner.transform, false);

    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 1000;

    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution    = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight     = 0.5f;
    scaler.referencePixelsPerUnit = 100f;
    canvasGo.AddComponent<GraphicRaycaster>();

    var hud = canvasGo.AddComponent<RuntimeHudBootstrap>();
    hud.StampLayoutVersion(RuntimeHudBootstrap.CurrentHudLayoutVersion);

    var view = RuntimeHudBootstrap.BuildCastBarPanel(canvasGo, uiFont, white);
    var bannerView = RuntimeHudBootstrap.BuildBannerPanel(canvasGo, uiFont);

    EditorUtility.SetDirty(hud);
    EditorUtility.SetDirty(view);
    EditorUtility.SetDirty(bannerView);
    EditorUtility.SetDirty(canvasGo);

    Debug.Log("ForbesFusionSharedSceneSetup: Created ForbesHudCanvas with RuntimeHudBootstrap, CastBarView, and CombatFeedbackBannerView.");
  }

}
#endif
