using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  [TestFixture]
  public class ActiveSpellInstanceRegistryTests {
    [Test]
    public void SpellInstanceKind_TargetedProjectile_IsZero() {
      Assert.AreEqual(0, (byte)SpellInstanceKind.TargetedProjectile);
    }

    [Test]
    public void ActiveSpellInstance_Default_SpellId_IsZero() {
      var inst = new ActiveSpellInstance();
      Assert.AreEqual(0, inst.SpellId, "SpellId 0 = inactive (no active instance).");
    }

    [Test]
    public void ActiveSpellInstance_IsActive_FalseWhenSpellIdZero() {
      var inst = new ActiveSpellInstance();
      Assert.IsFalse(inst.IsActive);
    }

    [Test]
    public void ActiveSpellInstanceRegistry_ExecutionOrder_IsLessThanNegative100() {
      var attr = typeof(ActiveSpellInstanceRegistry)
        .GetCustomAttributes(typeof(DefaultExecutionOrder), false)
        .Cast<DefaultExecutionOrder>()
        .FirstOrDefault();
      Assert.IsNotNull(attr, "Registry must declare [DefaultExecutionOrder].");
      Assert.Less(attr.order, -100,
        "Registry resolves before NetworkCombatController (-100).");
    }

    [Test]
    public void ActiveSpellInstanceRegistry_ExtendsNetworkBehaviour() {
      Assert.IsTrue(typeof(NetworkBehaviour).IsAssignableFrom(typeof(ActiveSpellInstanceRegistry)));
    }

    [Test]
    public void ActiveSpellInstanceRegistry_Capacity_Is16() {
      Assert.AreEqual(16, ActiveSpellInstanceRegistry.Capacity);
    }

    [Test]
    public void ActiveSpellInstance_IsActive_TrueWhenSpellIdNonZero() {
      var inst = new ActiveSpellInstance { SpellId = 1 };
      Assert.IsTrue(inst.IsActive);
    }

    [Test]
    public void ActiveSpellInstance_DefaultInstanceId_IsZero() {
      var inst = new ActiveSpellInstance();
      Assert.AreEqual(0, inst.InstanceId,
        "Default InstanceId must be 0 (unassigned).");
    }

    [Test]
    public void ActiveSpellInstance_InstanceIdDoesNotDriveIsActive() {
      var inst = new ActiveSpellInstance { SpellId = 0, InstanceId = 1 };
      Assert.IsFalse(inst.IsActive,
        "IsActive is governed by SpellId, not InstanceId.");
    }

    [Test]
    public void ActiveSpellInstanceRegistry_CanBeAddedToGameObject_WithoutThrowing() {
      var go = new GameObject(nameof(ActiveSpellInstanceRegistry_CanBeAddedToGameObject_WithoutThrowing));
      try {
        Assert.DoesNotThrow(() => go.AddComponent<ActiveSpellInstanceRegistry>(),
          "Adding ActiveSpellInstanceRegistry to a bare GameObject must not throw.");
      } finally {
        Object.DestroyImmediate(go);
      }
    }

    [Test]
    public void ActiveSpellInstancePresenter_RequiresRegistry() {
      var attrs = typeof(ActiveSpellInstancePresenter)
        .GetCustomAttributes(typeof(RequireComponent), false)
        .Cast<RequireComponent>()
        .ToList();

      bool requiresRegistry = attrs.Any(a =>
        a.m_Type0 == typeof(ActiveSpellInstanceRegistry) ||
        a.m_Type1 == typeof(ActiveSpellInstanceRegistry) ||
        a.m_Type2 == typeof(ActiveSpellInstanceRegistry));

      Assert.IsTrue(requiresRegistry,
        "ActiveSpellInstancePresenter must [RequireComponent(typeof(ActiveSpellInstanceRegistry))].");
    }

    /// <summary>
    /// Guards against Awake-time sphere creation side effects.
    /// Runtime presentation (LateUpdate) is covered by PlayMode smoke tests.
    /// </summary>
    [Test]
    public void ActiveSpellInstancePresenter_CanBeAddedToGameObject_WithoutThrowingOrSpawningSpheres() {
      var go = new GameObject("TestPresenter");
      go.AddComponent<ActiveSpellInstanceRegistry>();
      try {
        Assert.DoesNotThrow(() => go.AddComponent<ActiveSpellInstancePresenter>(),
          "Adding ActiveSpellInstancePresenter must not throw.");

        var spheres = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
          .Where(g => g != null && g.name == ActiveSpellInstancePresenter.ProjectileVisualName)
          .ToList();

        Assert.AreEqual(0, spheres.Count,
          $"Adding ActiveSpellInstancePresenter to a GameObject must not create '{ActiveSpellInstancePresenter.ProjectileVisualName}' visuals in Awake.");
      } finally {
        Object.DestroyImmediate(go);
      }
    }
  }
}
