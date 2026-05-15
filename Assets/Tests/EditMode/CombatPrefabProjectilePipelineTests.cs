using System.IO;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Ensures networked combat prefabs keep the projectile spell pipeline intact:
  /// every <see cref="NetworkCombatController"/> caster needs a replicated
  /// <see cref="ActiveSpellInstanceRegistry"/> and a local
  /// <see cref="ActiveSpellInstancePresenter"/> for orb visuals.
  /// </summary>
  public class CombatPrefabProjectilePipelineTests {
    static void AssertProjectilePipelineOnRoot(GameObject prefab, string label) {
      Assert.IsNotNull(prefab, $"{label}: prefab missing.");
      Assert.IsNotNull(prefab.GetComponent<NetworkCombatController>(),
        $"{label}: NetworkCombatController required.");
      Assert.IsNotNull(prefab.GetComponent<ActiveSpellInstanceRegistry>(),
        $"{label}: ActiveSpellInstanceRegistry required — without it, Fireball never enters simulation.");
      Assert.IsNotNull(prefab.GetComponent<ActiveSpellInstancePresenter>(),
        $"{label}: ActiveSpellInstancePresenter required — without it, clients see no projectile orb.");
    }

    [Test]
    public void TrainingDummyPrefab_OnRoot_HasProjectileSpellPipeline() {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TrainingDummy.prefab");
      AssertProjectilePipelineOnRoot(prefab, "TrainingDummy.prefab");
    }

    [Test]
    public void PlayerCharacterPrefab_OnRoot_HasProjectileSpellPipeline() {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlayerCharacter.prefab");
      AssertProjectilePipelineOnRoot(prefab, "PlayerCharacter.prefab");
    }

    [Test]
    public void PlayerCharacterPrefab_OnRoot_HasSpellImpactPrefabConfigured() {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlayerCharacter.prefab");
      Assert.IsNotNull(prefab, "PlayerCharacter.prefab missing.");

      var impactView = prefab.GetComponent<SpellImpactView>();
      Assert.IsNotNull(impactView,
        "PlayerCharacter.prefab requires SpellImpactView so spell impacts can show local-only visuals.");

      var serialized = new SerializedObject(impactView);
      var prop = serialized.FindProperty("_impactPrefab");
      Assert.IsNotNull(prop, "SpellImpactView._impactPrefab serialized field missing.");
      Assert.IsNotNull(prop.objectReferenceValue,
        "PlayerCharacter.prefab must wire SpellImpactView._impactPrefab; runtime primitive fallbacks are not allowed.");
      Assert.AreEqual(
        "Assets/Resources/Cosmetics/FireballImpactFlash.prefab",
        AssetDatabase.GetAssetPath(prop.objectReferenceValue),
        "Fireball impacts should use the explicit cosmetic impact prefab.");
    }

    [Test]
    public void FireballImpactFlashPrefab_HasNoCollider() {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Cosmetics/FireballImpactFlash.prefab");
      Assert.IsNotNull(prefab, "FireballImpactFlash.prefab missing.");
      Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true),
        "The first impact prefab should not include colliders; runtime still disables any colliders defensively.");
    }
  }

  public class SpellImpactViewPrefabPolicyTests {
    [Test]
    public void SpellImpactView_Source_DoesNotCreateRuntimePrimitive() {
      string src = File.ReadAllText(Path.Combine("Assets", "Scripts", "Combat", "SpellImpactView.cs"));
      StringAssert.DoesNotContain("GameObject.CreatePrimitive", src,
        "SpellImpactView must instantiate its configured impact prefab instead of creating runtime primitives.");
      StringAssert.DoesNotContain("PrimitiveType", src,
        "SpellImpactView must not keep a primitive fallback for missing impact prefabs.");
    }
  }

  /// <summary>
  /// Verifies the collider-removal policy for cosmetic projectile visuals:
  /// colliders must be disabled synchronously before the deferred <c>Destroy</c>
  /// so the visual never participates in a physics step after creation.
  /// </summary>
  public class ProjectileVisualColliderPolicyTests {
    [Test]
    public void DisableAndDestroyCollidersInChildren_DisablesRootColliderSynchronously() {
      var go = new GameObject("test-visual-root-collider");
      go.AddComponent<SphereCollider>();

      ActiveSpellInstancePresenter.DisableAndDestroyCollidersInChildren(go);

      Assert.IsFalse(go.GetComponent<SphereCollider>().enabled,
        "Root collider must be disabled synchronously so it leaves the physics simulation before Destroy runs.");

      Object.DestroyImmediate(go);
    }

    [Test]
    public void DisableAndDestroyCollidersInChildren_DisablesAllChildCollidersInHierarchy() {
      var root  = new GameObject("test-visual-root");
      var child = new GameObject("test-visual-child");
      child.transform.SetParent(root.transform);
      root.AddComponent<BoxCollider>();
      child.AddComponent<CapsuleCollider>();

      ActiveSpellInstancePresenter.DisableAndDestroyCollidersInChildren(root);

      Assert.IsFalse(root.GetComponent<BoxCollider>().enabled,
        "Root BoxCollider must be disabled synchronously.");
      Assert.IsFalse(child.GetComponent<CapsuleCollider>().enabled,
        "Child CapsuleCollider must be disabled synchronously.");

      Object.DestroyImmediate(root);
    }
  }
}
