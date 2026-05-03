using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  [TestFixture]
  public class TargetableTests {
    GameObject _root;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _root = new GameObject(nameof(TargetableTests) + "_Root");
    }

    [TearDown]
    public void TearDown() {
      if (_root != null) {
        Object.DestroyImmediate(_root);
        _root = null;
      }
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [Test]
    public void DisplayName_WithoutSerializedOverride_FallsBackToGameObjectName() {
      _root.name = "TrainingDummy_01";
      _root.AddComponent<NetworkObject>();
      var targetable = _root.AddComponent<Targetable>();
      Assert.AreEqual("TrainingDummy_01", targetable.DisplayName);
    }

    [Test]
    public void DisplayName_WithSerializedOverride_UsesOverride() {
      _root.AddComponent<NetworkObject>();
      var targetable = _root.AddComponent<Targetable>();
      var field = typeof(Targetable).GetField("_displayName",
        BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.IsNotNull(field);
      field.SetValue(targetable, "Boss");
      Assert.AreEqual("Boss", targetable.DisplayName);
    }

    [Test]
    public void NetworkObject_Property_LazilyCaches_FromGetComponent() {
      _root.AddComponent<NetworkObject>();
      var targetable = _root.AddComponent<Targetable>();
      var netField = typeof(Targetable).GetField("_netObj",
        BindingFlags.NonPublic | BindingFlags.Instance);
      Assert.IsNotNull(netField);
      netField.SetValue(targetable, null);
      var fromProp = targetable.NetworkObject;
      Assert.IsNotNull(fromProp);
      Assert.AreSame(fromProp, targetable.GetComponent<NetworkObject>());
    }

    [Test]
    public void Targetable_Has_NetworkObject_Sibling_FromRequireComponent() {
      var targetable = _root.AddComponent<Targetable>();
      Assert.IsNotNull(targetable.GetComponent<NetworkObject>());
    }
  }
}
