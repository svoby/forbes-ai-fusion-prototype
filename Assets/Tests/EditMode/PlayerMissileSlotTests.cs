using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Structural tests for <see cref="PlayerMissileSlot"/>.
  /// <para>
  /// <see cref="PlayerMissileSlot"/> is a <see cref="NetworkBehaviour"/>; its
  /// <c>[Networked]</c> properties require a live Fusion runner and cannot be
  /// exercised here. Schedule / Clear / impact-event correctness is covered by
  /// <c>NetworkCombatProjectileTravelSmokeTests</c> (PlayMode).
  /// </para>
  /// </summary>
  [TestFixture]
  public class PlayerMissileSlotTests {
    /// <summary>
    /// <see cref="PlayerMissileSlot"/> must carry <c>[DefaultExecutionOrder(-200)]</c>
    /// so it resolves in-flight missiles <em>before</em>
    /// <see cref="NetworkCombatController"/> (<c>-100</c>) processes new player input
    /// on the same tick — preserving the resolve-before-schedule invariant.
    /// </summary>
    [Test]
    public void PlayerMissileSlot_ExecutionOrder_IsLessThanNegative100() {
      var attr = typeof(PlayerMissileSlot)
        .GetCustomAttributes(typeof(DefaultExecutionOrder), false)
        .Cast<DefaultExecutionOrder>()
        .FirstOrDefault();

      Assert.IsNotNull(attr,
        "PlayerMissileSlot must declare [DefaultExecutionOrder] so Fusion respects " +
        "its tick ordering relative to NetworkCombatController.");
      Assert.Less(attr.order, -100,
        "PlayerMissileSlot execution order must be less than NetworkCombatController (-100) " +
        "so TryResolvePendingImpact runs before new casts are processed.");
    }

    /// <summary>
    /// <see cref="PlayerMissileSlot"/> must extend <see cref="NetworkBehaviour"/>
    /// so its replicated fields (<c>PendingImpactSpellId</c>, <c>PendingImpactTarget</c>,
    /// <c>PendingMissileReleaseTick</c>, <c>MissileOrigin</c>) are observed by all
    /// clients and used to drive <see cref="CosmeticProjectileView"/> locally.
    /// </summary>
    [Test]
    public void PlayerMissileSlot_ExtendsNetworkBehaviour() {
      Assert.IsTrue(
        typeof(NetworkBehaviour).IsAssignableFrom(typeof(PlayerMissileSlot)),
        "PlayerMissileSlot must extend NetworkBehaviour to replicate missile travel state.");
    }

    /// <summary>
    /// <see cref="PlayerMissileSlot"/> can be added to a bare <see cref="GameObject"/>
    /// without throwing. This guards against Awake-time side effects that would break
    /// the test environment.
    /// </summary>
    [Test]
    public void PlayerMissileSlot_CanBeAddedToGameObject_WithoutThrowing() {
      var go = new GameObject(nameof(PlayerMissileSlot_CanBeAddedToGameObject_WithoutThrowing));
      try {
        Assert.DoesNotThrow(() => go.AddComponent<PlayerMissileSlot>(),
          "Adding PlayerMissileSlot to a bare GameObject must not throw.");
      } finally {
        Object.DestroyImmediate(go);
      }
    }

    /// <summary>
    /// <see cref="CosmeticProjectileView"/> must require <see cref="PlayerMissileSlot"/>
    /// (not <see cref="NetworkCombatController"/>) after the missile-slot extraction.
    /// The view reads missile travel state from <see cref="PlayerMissileSlot"/> only.
    /// </summary>
    [Test]
    public void CosmeticProjectileView_RequiresPlayerMissileSlot_NotNCC() {
      var attrs = typeof(CosmeticProjectileView)
        .GetCustomAttributes(typeof(RequireComponent), false)
        .Cast<RequireComponent>()
        .ToList();

      var requiresSlot = attrs.Any(a =>
        a.m_Type0 == typeof(PlayerMissileSlot) ||
        a.m_Type1 == typeof(PlayerMissileSlot) ||
        a.m_Type2 == typeof(PlayerMissileSlot));

      var requiresNcc = attrs.Any(a =>
        a.m_Type0 == typeof(NetworkCombatController) ||
        a.m_Type1 == typeof(NetworkCombatController) ||
        a.m_Type2 == typeof(NetworkCombatController));

      Assert.IsTrue(requiresSlot,
        "CosmeticProjectileView must [RequireComponent(typeof(PlayerMissileSlot))].");
      Assert.IsFalse(requiresNcc,
        "CosmeticProjectileView must not require NetworkCombatController after missile-slot extraction.");
    }
  }
}
