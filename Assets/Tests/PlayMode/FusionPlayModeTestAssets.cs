#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Loads runtime prefabs by asset path (PlayMode tests run in the Editor).
  /// </summary>
  internal static class FusionPlayModeTestAssets {
    internal const string PlayerCharacterPrefabPath = "Assets/PlayerCharacter.prefab";
    internal const string TrainingDummyPrefabPath = "Assets/TrainingDummy.prefab";

    internal static GameObject LoadPrefab(string assetPath) {
#if UNITY_EDITOR
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
      if (prefab == null) {
        Debug.LogError($"FusionPlayModeTestAssets: missing prefab at {assetPath}");
      }

      return prefab;
#else
      return null;
#endif
    }
  }
}
