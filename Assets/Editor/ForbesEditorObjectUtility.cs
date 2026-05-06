#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared editor-only object helpers for scene and prefab setup code.
/// </summary>
public static class ForbesEditorObjectUtility {
  public static T EnsureComponent<T>(GameObject go) where T : Component {
    var component = go.GetComponent<T>();
    if (component == null) {
      component = Undo.AddComponent<T>(go);
    }

    return component;
  }

  public static void WireAssetRef<T>(T component, string propertyName, string assetPath) where T : Object {
    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    if (asset == null) {
      Debug.LogWarning($"ForbesEditorObjectUtility: Asset not found at {assetPath}; wire {propertyName} manually.");
      return;
    }

    var so = new SerializedObject(component);
    var prop = so.FindProperty(propertyName);
    if (prop == null) {
      Debug.LogWarning($"ForbesEditorObjectUtility: Property '{propertyName}' not found on {typeof(T).Name}.");
      return;
    }

    prop.objectReferenceValue = asset;
    so.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(component);
  }
}
#endif
