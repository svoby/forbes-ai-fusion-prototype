using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Drives <see cref="Health.IsDeadChanged"/> without a network session by invoking the compiler-generated
  /// event delegate field from tests.
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

      InvokeIsDeadChanged(health, true);
      Assert.IsFalse(renderer.enabled);
    }

    [Test]
    public void IsDeadChanged_False_ReEnablesRootRenderer() {
      var renderer = _root.AddComponent<MeshRenderer>();
      _root.AddComponent<NetworkObject>();
      var health = _root.AddComponent<Health>();
      var view = _root.AddComponent<HealthView>();
      view.enabled = true;

      InvokeIsDeadChanged(health, true);
      InvokeIsDeadChanged(health, false);
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
        InvokeIsDeadChanged(health, true);
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

      Assert.DoesNotThrow(() => InvokeIsDeadChanged(health, true));
      Assert.IsTrue(renderer.enabled);
    }

    static void InvokeIsDeadChanged(Health health, bool isDead) {
      var field = typeof(Health).GetField("IsDeadChanged",
        BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.IsNotNull(field, "Could not resolve backing field for Health.IsDeadChanged.");
      var dlg = (Action<bool>)field.GetValue(health);
      dlg?.Invoke(isDead);
    }
  }
}
