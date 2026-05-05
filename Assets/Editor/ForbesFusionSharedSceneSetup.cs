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

    // Replace a legacy single-plane Floor with the checkerboard grid.
    if (existing != null && existing.transform.childCount == 0) {
      Undo.DestroyObjectImmediate(existing);
      existing = null;
    }

    int expectedTiles = CheckerboardFloor.GridDimension * CheckerboardFloor.GridDimension;
    if (existing != null && existing.transform.childCount == expectedTiles) return;

    if (existing != null) {
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
    var spawner = EnsureComponent<PlayerSpawner>(go);
    EnsureComponent<KeyboardInputSource>(go);
    EnsureComponent<FusionInputProvider>(go);
    EnsureComponent<TargetingController>(go);
    EnsureComponent<SelectedTargetHealthBar>(go);
    var dummySpawner = EnsureComponent<TrainingDummySpawner>(go);
    EnsureComponent<CombatHud>(go);
    EnsureComponent<FusionHudToggle>(go);
    EnsureHudCanvas(runner);

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
  //  HUD canvas (cast bar)
  // -----------------------------------------------------------------------

  static Transform FindHudCanvasDirectChild(NetworkRunner runner) {
    Transform t = runner.transform;
    for (var i = 0; i < t.childCount; i++) {
      Transform c = t.GetChild(i);
      if (c != null && c.name == CastBarView.HudCanvasChildName) {
        return c;
      }
    }

    return null;
  }

  /// <summary>
  /// One overlay canvas under the runner with <see cref="CastBarView"/> wired.
  /// Skips creation if <see cref="CastBarView.HudCanvasChildName"/> already exists. Cast bar overlay does not create an EventSystem (avoids Input System UI module clashes and is not needed for passive rendering).
  /// </summary>
  static void EnsureHudCanvas(NetworkRunner runner) {
    if (FindHudCanvasDirectChild(runner) != null) {
      return;
    }

    Font uiFont = CastBarView.ResolveDefaultHudFont(26);

    var white = Sprite.Create(
      Texture2D.whiteTexture,
      new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
      new Vector2(0.5f, 0.5f),
      100f);

    GameObject canvasGo = new GameObject(CastBarView.HudCanvasChildName);
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

    GameObject panel = new GameObject("CastBarPanel");
    Undo.RegisterCreatedObjectUndo(panel, "CastBarPanel");
    panel.transform.SetParent(canvasGo.transform, false);
    var panelRect = panel.AddComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0.5f, 0f);
    panelRect.anchorMax = new Vector2(0.5f, 0f);
    panelRect.pivot = new Vector2(0.5f, 0f);
    panelRect.anchoredPosition = new Vector2(0f, CastBarView.CastBarLiftFromBottomPx);
    panelRect.sizeDelta = new Vector2(CastBarView.CastBarPanelWidth, CastBarView.CastBarPanelHeight);
    var panelGroup = panel.AddComponent<CanvasGroup>();
    panelGroup.alpha = 0f;
    panelGroup.blocksRaycasts = false;

    var bg = new GameObject("Background");
    Undo.RegisterCreatedObjectUndo(bg, "CastBar Background");
    bg.transform.SetParent(panel.transform, false);
    var bgRt = bg.AddComponent<RectTransform>();
    StretchFull(bgRt);
    var bgImg = bg.AddComponent<Image>();
    bgImg.sprite = white;
    bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

    var nameGo = new GameObject("SpellName");
    Undo.RegisterCreatedObjectUndo(nameGo, "CastBar SpellName");
    nameGo.transform.SetParent(panel.transform, false);
    var nameRt = nameGo.AddComponent<RectTransform>();
    nameRt.anchorMin = new Vector2(0f, 1f);
    nameRt.anchorMax = new Vector2(1f, 1f);
    nameRt.pivot = new Vector2(0.5f, 1f);
    nameRt.anchoredPosition = new Vector2(0f, -6f);
    nameRt.sizeDelta = new Vector2(-24f, 40f);
    var nameTxt = nameGo.AddComponent<Text>();
    CastBarView.StyleHudText(nameTxt, uiFont, 26, FontStyle.Bold, Color.white);
    nameTxt.alignment = TextAnchor.MiddleCenter;
    nameTxt.text = "";

    var trackGo = new GameObject("FillTrack");
    Undo.RegisterCreatedObjectUndo(trackGo, "CastBar FillTrack");
    trackGo.transform.SetParent(panel.transform, false);
    var trackRt = trackGo.AddComponent<RectTransform>();
    trackRt.anchorMin = new Vector2(0f, 0f);
    trackRt.anchorMax = new Vector2(1f, 0f);
    trackRt.pivot = new Vector2(0.5f, 0f);
    trackRt.anchoredPosition = new Vector2(0f, 20f);
    trackRt.sizeDelta = new Vector2(-24f, 30f);
    var trackImg = trackGo.AddComponent<Image>();
    trackImg.sprite = white;
    trackImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);

    var fillGo = new GameObject("Fill");
    Undo.RegisterCreatedObjectUndo(fillGo, "CastBar Fill");
    fillGo.transform.SetParent(trackGo.transform, false);
    var fillRt = fillGo.AddComponent<RectTransform>();
    StretchFull(fillRt);
    var fillImg = fillGo.AddComponent<Image>();
    fillImg.sprite = white;
    fillImg.type = Image.Type.Filled;
    fillImg.fillMethod = Image.FillMethod.Horizontal;
    fillImg.fillOrigin = 0;
    fillImg.color = new Color(0.85f, 0.45f, 0.12f, 1f);
    fillImg.fillAmount = 0f;

    var view = canvasGo.AddComponent<CastBarView>();
    view.BindUi(panelGroup, fillImg, nameTxt);
    view.StampLayoutVersion(CastBarView.CurrentHudLayoutVersion);

    EditorUtility.SetDirty(view);
    EditorUtility.SetDirty(canvasGo);

    Debug.Log("ForbesFusionSharedSceneSetup: Created ForbesHudCanvas with CastBarView.");
  }

  static void StretchFull(RectTransform rt) {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    rt.localScale = Vector3.one;
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
