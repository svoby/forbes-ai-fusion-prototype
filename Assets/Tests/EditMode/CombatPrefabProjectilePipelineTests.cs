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
  }
}
