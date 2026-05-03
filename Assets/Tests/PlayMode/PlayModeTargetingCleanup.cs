using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Removes auto-bootstrapped targeting objects so PlayMode tests do not share global singleton state.
  /// </summary>
  internal static class PlayModeTargetingCleanup {
    const string TargetingSystemName = "[TargetingSystem]";

    internal static void DestroyAutoCreatedTargetingSystem() {
      var go = GameObject.Find(TargetingSystemName);
      if (go != null) {
        Object.DestroyImmediate(go);
      }
    }
  }
}
