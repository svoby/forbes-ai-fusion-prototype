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

    [SetUp]
    public void SetUp() {
      _go = new GameObject(nameof(CombatHitEventTests));
      _go.AddComponent<Health>();  // required by HitImpactView [RequireComponent]
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
    /// <see cref="FloatingCombatTextCanvas.ShowDamage"/> is a no-op outside play mode
    /// (<c>!Application.isPlaying</c>), so no canvas or TextMesh is created.
    /// </summary>
    [Test]
    public void HitImpactView_OnCombatHitReceived_DoesNotCreateWorldSpaceTextMesh() {
      var view = _go.AddComponent<HitImpactView>();

      // EditMode tests do not call Awake/OnEnable for behaviours without [ExecuteAlways],
      // so we cannot drive the full subscription lifecycle here. Invoke OnHit directly
      // to verify the two behavioural contracts: no throw, no world-space TextMesh.
      var onHit = typeof(HitImpactView)
        .GetMethod("OnHit", BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.IsNotNull(onHit, "HitImpactView.OnHit was renamed or removed.");

      int meshCountBefore = UnityEngine.Object.FindObjectsByType<TextMesh>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

      Assert.DoesNotThrow(() => onHit.Invoke(view, new object[] { 42f }),
        "HitImpactView.OnHit must not throw when Camera.main is null.");

      int meshCountAfter = UnityEngine.Object.FindObjectsByType<TextMesh>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

      Assert.AreEqual(meshCountBefore, meshCountAfter,
        "HitImpactView must not create world-space TextMesh objects; " +
        "floating damage text is now screen-space UI via FloatingCombatTextCanvas.");
    }
  }
}
