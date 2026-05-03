using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Clears ambient singleton-ish scene junk between PlayMode tests so unrelated fixtures do not inherit broken state.
  /// </summary>
  internal static class PlayModeTargetingCleanup {
    const string TargetingSystemName = "[TargetingSystem]";

    internal static void DestroyAutoCreatedTargetingSystem() {
      var go = GameObject.Find(TargetingSystemName);
      if (go != null) {
        Object.DestroyImmediate(go);
      }

      DestroyOrphanedFusionPlayModeRunnerHosts();
    }

    /// <summary>
    /// Fusion smoke fixtures leave this GameObject name behind when failing mid-test; later TargetHighlight/Targetable tests then simulate alongside an orphaned runner.
    /// </summary>
    static void DestroyOrphanedFusionPlayModeRunnerHosts() {
      const string prefix = nameof(FusionSinglePlayerTestSession);

      var transforms = Object.FindObjectsByType<Transform>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None);

      for (var i = 0; i < transforms.Length; i++) {
        Transform tr = transforms[i];
        if (tr == null || tr.parent != null) {
          continue;
        }

        GameObject root = tr.gameObject;
        if (root != null && root.name.StartsWith(prefix)) {
          Object.DestroyImmediate(root);
        }
      }
    }
  }
}
