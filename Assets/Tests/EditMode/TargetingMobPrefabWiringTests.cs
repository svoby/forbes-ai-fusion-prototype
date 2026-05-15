using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Guards serialized mob wiring needed for Fusion-spawn targets to participate in PhysicsScene raycasts.
  /// </summary>
  public class TargetingMobPrefabWiringTests {
    static void AssertClickTargetablePrefabRoot(GameObject root, string assetLabel) {
      Assert.IsNotNull(root, $"{assetLabel}: prefab missing.");
      Assert.IsNotNull(root.GetComponent<NetworkObject>(),
        $"{assetLabel}: NetworkObject prefab wiring required for networked targeting IDs.");

      var targetable = root.GetComponent<Targetable>();
      Assert.IsNotNull(targetable, $"{assetLabel}: Targetable marker required so Tab targeting can enumerate mobs.");

      var colliders = root.GetComponentsInChildren<Collider>(true);
      Assert.IsFalse(colliders.Length == 0,
        $"{assetLabel}: needs at least one Collider child or root attached so LMB raycasts can resolve the mob.");
      Assert.IsFalse(colliders.Any(c => !c.enabled),
        $"{assetLabel}: targeting colliders must stay enabled.");

      AssertPhysicsQueryableCollider(assetLabel, colliders);
    }

    static void AssertPhysicsQueryableCollider(string label, Collider[] colliders) {
      bool usable = colliders.Any(c => c.enabled);
      Assert.IsTrue(usable,
        $"{label}: needs at least one enabled collider (trigger volumes are acceptable for LMB targeting rays).");
    }

    [Test]
    public void TrainingDummyPrefab_OnRoot_HasTargetableColliderAndFusionIdentity() {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TrainingDummy.prefab");
      Assert.IsNotNull(prefab, "TrainingDummy.prefab asset path drifted.");
      AssertClickTargetablePrefabRoot(prefab, "TrainingDummy.prefab");
      var proxy = prefab.GetComponentInChildren<CapsuleCollider>();
      Assert.IsNotNull(proxy,
        "TrainingDummy.prefab relies on an explicit CapsuleCollider proxy volume for deterministic Fusion/Default physics rays.");
      Assert.IsTrue(proxy.isTrigger,
        "TrainingDummy.prefab CapsuleCollider must remain a trigger so CharacterController bodies are not squeezed by solid overlap.");
    }
  }
}
