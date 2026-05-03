using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Exercises <see cref="HealthView"/> dead visuals by invoking its private apply hook (stable vs.event backing-field reflection).
  /// </summary>
  [TestFixture]
  public class HealthViewTests {
    GameObject _root;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _root = new GameObject(nameof(HealthViewTests) + "_Root");
    }

    [TearDown]
    public void TearDown() {
      if (_root != null) {
        UnityEngine.Object.DestroyImmediate(_root);
        _root = null;
      }
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [Test]
    public void IsDeadChanged_True_DisablesRootRenderer() {
      var renderer = _root.AddComponent<MeshRenderer>();
      _root.AddComponent<NetworkObject>();
      var health = _root.AddComponent<Health>();
      var view = _root.AddComponent<HealthView>();
      view.enabled = true;

      InvokeApplyDeadVisual(view, true);
      Assert.IsFalse(renderer.enabled);
    }

    [Test]
    public void IsDeadChanged_False_ReEnablesRootRenderer() {
      var renderer = _root.AddComponent<MeshRenderer>();
      _root.AddComponent<NetworkObject>();
      var health = _root.AddComponent<Health>();
      var view = _root.AddComponent<HealthView>();
      view.enabled = true;

      InvokeApplyDeadVisual(view, true);
      InvokeApplyDeadVisual(view, false);
      Assert.IsTrue(renderer.enabled);
    }

    [Test]
    public void IsDeadChanged_True_DisablesChildRenderers() {
      var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
      child.transform.SetParent(_root.transform, worldPositionStays: false);
      var childRenderer = child.GetComponent<MeshRenderer>();
      var rootRenderer = _root.AddComponent<MeshRenderer>();
      _root.AddComponent<NetworkObject>();
      var health = _root.AddComponent<Health>();
      var view = _root.AddComponent<HealthView>();
      view.enabled = true;

      try {
        InvokeApplyDeadVisual(view, true);
        Assert.IsFalse(rootRenderer.enabled);
        Assert.IsFalse(childRenderer.enabled);
      } finally {
        UnityEngine.Object.DestroyImmediate(child);
      }
    }

    [Test]
    public void HealthViewDisabled_IsDeadChanged_DoesNotChangeRenderers() {
      var renderer = _root.AddComponent<MeshRenderer>();
      _root.AddComponent<NetworkObject>();
      var health = _root.AddComponent<Health>();
      var view = _root.AddComponent<HealthView>();
      view.enabled = true;
      view.enabled = false;

      Assert.DoesNotThrow(() => InvokeHealthIsDeadChangedMulticast(health, true));
      Assert.IsTrue(renderer.enabled);
    }

    /// <summary>
    /// Raises <see cref="Health.IsDeadChanged"/> like Fusion/network code would — respects unsubscribes when <see cref="HealthView"/> disables.
    /// </summary>
    static void InvokeHealthIsDeadChangedMulticast(Health health, bool isDead) {
      foreach (var fi in typeof(Health).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
        if (fi.FieldType != typeof(Action<bool>)) {
          continue;
        }

        if (!fi.Name.Contains("IsDeadChanged")) {
          continue;
        }

        var dlg = (Action<bool>)fi.GetValue(health);
        dlg?.Invoke(isDead);
        return;
      }

      Assert.Fail("Could not find IsDeadChanged backing field on Health (Fusion/weaver rename?).");
    }

    static void InvokeApplyDeadVisual(HealthView view, bool isDead) {
      var mi = typeof(HealthView).GetMethod("ApplyDeadVisual",
        BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.IsNotNull(mi, "ApplyDeadVisual must stay test-visible (private instance method).");
      mi.Invoke(view, new object[] { isDead });
    }
  }
}
