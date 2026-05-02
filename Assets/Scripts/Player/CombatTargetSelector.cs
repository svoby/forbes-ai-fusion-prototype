using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Tab-target list: collects other alive networked <see cref="Health"/> actors (plain C#, no <see cref="NetworkBehaviour"/>).
/// </summary>
public static class CombatTargetSelector {
  public static void CollectAliveOthers(NetworkObject self, List<Health> into) {
    into.Clear();
    foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) {
      if (h.Object == null || h.Object == self || h.IsDead) {
        continue;
      }

      into.Add(h);
    }

    into.Sort(static (a, b) => a.Object.Id.Raw.CompareTo(b.Object.Id.Raw));
  }

  /// <summary>
  /// Picks the next target in stable sorted order, or <see cref="NetworkId"/> default if none.
  /// </summary>
  public static NetworkId SelectNextAfter(NetworkObject self, NetworkId current, List<Health> scratch) {
    CollectAliveOthers(self, scratch);
    if (scratch.Count == 0) {
      return default;
    }

    int idx = 0;
    if (current.IsValid) {
      idx = scratch.FindIndex(h => h.Object.Id == current);
      if (idx < 0) {
        idx = 0;
      } else {
        idx = (idx + 1) % scratch.Count;
      }
    }

    return scratch[idx].Object.Id;
  }
}
