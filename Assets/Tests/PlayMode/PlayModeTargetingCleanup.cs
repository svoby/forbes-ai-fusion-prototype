using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Clears ambient singleton-ish scene junk between PlayMode tests so unrelated fixtures do not inherit broken state.
  /// </summary>
  internal static class PlayModeTargetingCleanup {
    const string TargetingSystemName = "[TargetingSystem]";

    /// <summary>
    /// Removes every root named <c>[TargetingSystem]</c> (active or inactive). Optionally uses
    /// <see cref="Object.Destroy"/> instead of <see cref="Object.DestroyImmediate"/> — required
    /// mid-session while Fusion's <see cref="NetworkRunner"/> is active.
    /// </summary>
    internal static void DestroyAllTargetingSystemRoots(bool immediate) {
      var transforms = Object.FindObjectsByType<Transform>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None);

      for (var i = 0; i < transforms.Length; i++) {
        Transform tr = transforms[i];
        if (tr == null || tr.parent != null) {
          continue;
        }

        GameObject root = tr.gameObject;
        if (root == null || root.name != TargetingSystemName) {
          continue;
        }

        if (immediate) {
          Object.DestroyImmediate(root);
        } else {
          Object.Destroy(root);
        }
      }
    }

    internal static void DestroyAutoCreatedTargetingSystem() {
      DestroyAllTargetingSystemRoots(immediate: true);
      DestroyOrphanedFusionPlayModeRunnerHosts();
    }

    /// <summary>
    /// Call while a <see cref="FusionSinglePlayerTestSession"/> is running: removes duplicate targeting
    /// harnesses only. Does not run orphan-runner cleanup (that would destroy the active session host).
    /// </summary>
    internal static void DestroyTargetingSystemsDuringFusionSession() {
      DestroyAllTargetingSystemRoots(immediate: false);
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
