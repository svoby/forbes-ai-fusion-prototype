using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins <see cref="CosmeticProjectileView"/> collider handling: placeholder spheres from
  /// <c>GameObject.CreatePrimitive</c> must not expose an enabled collider after Awake
  /// (<c>docs/PROJECTILE_POLICY.md</c> §4a).
  /// </summary>
  [TestFixture]
  public class CosmeticProjectileViewColliderTests {
    GameObject _go;

    [SetUp]
    public void SetUp() {
      _go = new GameObject(nameof(CosmeticProjectileViewColliderTests));
      _go.AddComponent<Health>();
      _go.AddComponent<NetworkCombatController>();
      _go.AddComponent<CosmeticProjectileView>();
    }

    [TearDown]
    public void TearDown() {
      if (_go != null) {
        Object.DestroyImmediate(_go);
      }
    }

    [Test]
    public void CosmeticProjectileVisual_SphereNeverHasEnabledCollider_AfterAwake() {
      GameObject sphere = null;
      foreach (var candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)) {
        if (candidate != null && candidate.name == "FireballVisual") {
          sphere = candidate;
          break;
        }
      }

      Assert.IsNotNull(sphere,
        "CosmeticProjectileView should create the FireballVisual sphere in Awake.");

      var col = sphere.GetComponent<Collider>();
      Assert.IsTrue(col == null || !col.enabled,
        "Cosmetic projectile placeholder must not leave an enabled collider after setup " +
        "(disable synchronously before Destroy; see PROJECTILE_POLICY §4a).");
    }
  }
}
