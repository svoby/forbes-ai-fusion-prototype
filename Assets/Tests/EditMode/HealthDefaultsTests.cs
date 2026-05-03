using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins the public surface of <see cref="Health"/>: starting HP, respawn delay,
  /// the death-event signature, and the [Networked] properties whose existence
  /// the Fusion code-gen depends on. None of these touch network state, so the
  /// component runs fine without a NetworkRunner.
  /// </summary>
  [TestFixture]
  public class HealthDefaultsTests {
    GameObject _go;
    Health     _health;

    [SetUp]
    public void SetUp() {
      _go = new GameObject(nameof(HealthDefaultsTests));
      _health = _go.AddComponent<Health>();
    }

    [TearDown]
    public void TearDown() {
      if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
    }

    [Test]
    public void FreshlyConstructed_StartingHealthIs100_AndRespawnDelayIs3() {
      Assert.AreEqual(100f, _health.StartingHealth,
        "Health.StartingHealth changed; tests/HUD assume 100. Update SpellRegistry damage if intentional.");
      Assert.AreEqual(3f, _health.RespawnDelaySeconds,
        "Health.RespawnDelaySeconds changed; tick math in DealDamageRpc assumes 3 s.");
    }

    [Test]
    public void IsDeadChanged_IsPublicEventActionOfBool() {
      var ev = typeof(Health).GetEvent("IsDeadChanged",
        BindingFlags.Public | BindingFlags.Instance);
      Assert.IsNotNull(ev, "Health.IsDeadChanged event was renamed or removed.");
      Assert.AreEqual(typeof(Action<bool>), ev.EventHandlerType,
        "Health.IsDeadChanged signature drifted from Action<bool>; HealthView subscribers will break.");
    }

    [TestCase("NetworkedHealth", typeof(float))]
    [TestCase("IsDead",          typeof(NetworkBool))]
    [TestCase("RespawnAtTick",   typeof(int))]
    [TestCase("SpawnPosition",   typeof(Vector3))]
    public void NetworkedProperty_ExistsWithPublicSetter(string name, Type expectedType) {
      var prop = typeof(Health).GetProperty(name,
        BindingFlags.Public | BindingFlags.Instance);
      Assert.IsNotNull(prop, $"Health.{name} is missing; Fusion code-gen requires it.");
      Assert.AreEqual(expectedType, prop.PropertyType,
        $"Health.{name} type changed from {expectedType.Name}; networked layout will break.");
      Assert.IsTrue(prop.CanWrite, $"Health.{name} must have a setter for [Networked] code-gen.");
      var setter = prop.GetSetMethod(nonPublic: false);
      Assert.IsNotNull(setter, $"Health.{name} setter must be public.");
      Assert.IsTrue(setter.IsPublic, $"Health.{name} setter must be public.");
    }
  }
}
