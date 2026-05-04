using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins the public surface of the combat hit event on <see cref="Health"/>:
  /// <see cref="Health.CombatHitReceived"/> and the three replicated feedback
  /// properties — <see cref="Health.LastHitEventSeq"/>, <see cref="Health.LastHitDamage"/>,
  /// <see cref="Health.LastHitTick"/>. Also covers <see cref="HitImpactView"/>.
  /// Does not require a NetworkRunner.
  /// </summary>
  [TestFixture]
  public class CombatHitEventTests {
    GameObject _go;
    Health     _health;

    [SetUp]
    public void SetUp() {
      _go     = new GameObject(nameof(CombatHitEventTests));
      _health = _go.AddComponent<Health>();
    }

    [TearDown]
    public void TearDown() {
      if (_go != null) {
        UnityEngine.Object.DestroyImmediate(_go);
      }
    }

    // ── Health event surface ──────────────────────────────────────────────────

    [Test]
    public void CombatHitReceived_IsPublicEventActionOfFloat() {
      var ev = typeof(Health).GetEvent(
        "CombatHitReceived",
        BindingFlags.Public | BindingFlags.Instance);
      Assert.IsNotNull(ev, "Health.CombatHitReceived event was renamed or removed.");
      Assert.AreEqual(typeof(Action<float>), ev.EventHandlerType,
        "Health.CombatHitReceived signature must be Action<float>; HitImpactView subscribers will break.");
    }

    [TestCase("LastHitEventSeq", typeof(byte))]
    [TestCase("LastHitDamage",   typeof(float))]
    [TestCase("LastHitTick",     typeof(int))]
    public void NetworkedHitEventProperty_ExistsWithPublicSetter(string name, Type expectedType) {
      var prop = typeof(Health).GetProperty(
        name,
        BindingFlags.Public | BindingFlags.Instance);
      Assert.IsNotNull(prop, $"Health.{name} is missing; Fusion code-gen requires it.");
      Assert.AreEqual(expectedType, prop.PropertyType,
        $"Health.{name} type changed from {expectedType.Name}; networked layout will break.");
      Assert.IsTrue(prop.CanWrite, $"Health.{name} must have a setter for [Networked] code-gen.");
      Assert.IsNotNull(prop.GetSetMethod(nonPublic: false),
        $"Health.{name} setter must be public.");
    }

    // ── HitImpactView ─────────────────────────────────────────────────────────

    [Test]
    public void HitImpactView_CanBeAddedToGameObjectAlongsideHealth() {
      _go.AddComponent<HitImpactView>();
      Assert.IsNotNull(_go.GetComponent<HitImpactView>(),
        "HitImpactView should be addable to a GameObject that already has Health.");
    }

    [Test]
    public void HitImpactView_EnableDisableCycle_DoesNotThrow() {
      var view = _go.AddComponent<HitImpactView>();
      Assert.DoesNotThrow(() => {
        view.enabled = false;
        view.enabled = true;
      }, "HitImpactView enable/disable should not throw even without a runner.");
    }

    /// <summary>
    /// Firing CombatHitReceived must not throw and must NOT create any world-space
    /// TextMesh. Floating damage numbers are now rendered via
    /// <see cref="FloatingCombatTextCanvas"/> (screen-space UI).
    /// Camera.main is null in EditMode tests, so ShowDamage returns immediately
    /// and no canvas is created — verifying the null-camera guard.
    /// </summary>
    [Test]
    public void HitImpactView_OnCombatHitReceived_DoesNotCreateWorldSpaceTextMesh() {
      _go.AddComponent<HitImpactView>();

      var backingField = typeof(Health).GetField(
        "CombatHitReceived",
        BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.IsNotNull(backingField, "Cannot find CombatHitReceived backing field.");

      var del = backingField.GetValue(_health) as Action<float>;
      Assert.IsNotNull(del, "No subscribers registered; HitImpactView.OnEnable may have failed.");

      int meshCountBefore = UnityEngine.Object.FindObjectsByType<TextMesh>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

      Assert.DoesNotThrow(() => del.Invoke(42f),
        "HitImpactView.OnHit should not throw when Camera.main is null.");

      int meshCountAfter = UnityEngine.Object.FindObjectsByType<TextMesh>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

      Assert.AreEqual(meshCountBefore, meshCountAfter,
        "HitImpactView must not create world-space TextMesh objects; " +
        "floating damage text is now screen-space UI via FloatingCombatTextCanvas.");
    }
  }
}
