using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins the load-bearing rejection order in <see cref="CombatValidator.TryValidate"/>.
  /// Uses the seam-1 pure overload which takes a pre-resolved
  /// (<see cref="Transform"/> targetTransform, bool isTargetDead) instead of a
  /// runner-resolved <see cref="Health"/>, so every rule is exercisable in
  /// EditMode without spinning up a NetworkRunner. The Given/When/Then bullets
  /// in docs/TEST_COVERAGE_PLAN.md section E are mapped 1:1 below.
  /// </summary>
  [TestFixture]
  public class CombatValidatorPureTests {
    readonly List<GameObject> _spawned = new();

    Transform NewTransform(string name, Vector3 position) {
      var go = new GameObject(name);
      go.transform.position = position;
      _spawned.Add(go);
      return go.transform;
    }

    [TearDown]
    public void TearDown() {
      foreach (var go in _spawned) {
        if (go != null) Object.DestroyImmediate(go);
      }
      _spawned.Clear();
    }

    static SpellData MakeSpell(float range = 30f, bool triggersGcd = true) {
      return new SpellData(id: 99, name: "TestSpell", castTimeSec: 0f, cooldownSec: 0f,
        rangeMeters: range, damage: 10f, triggersGcd: triggersGcd);
    }

    static NetworkId ValidId(uint raw) {
      // NetworkId is a struct backed by a uint; reinterpret cast keeps tests
      // independent of internal field naming.
      return new NetworkId { Raw = raw };
    }

    [Test]
    public void IsAlreadyCasting_BeatsEverything() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", new Vector3(1000f, 0f, 0f)); // very far
      var spell  = MakeSpell(range: 5f);

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(42), spell,
        currentTick: 0, gcdEndTick: 9999, cooldownEndTick: 9999,
        isAlreadyCasting: true,
        targetTransform: target, isTargetDead: true,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.AlreadyCasting, failReason,
        "AlreadyCasting must short-circuit before any other rule.");
    }

    [Test]
    public void GcdActive_WhenSpellTriggersGcd_AndCurrentTickBeforeGcdEnd() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", Vector3.zero);
      var spell  = MakeSpell(triggersGcd: true);

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(1), spell,
        currentTick: 50, gcdEndTick: 100, cooldownEndTick: 0,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: false,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.GcdActive, failReason);
    }

    [Test]
    public void GcdIgnored_WhenSpellDoesNotTriggerGcd() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", Vector3.zero);
      var spell  = MakeSpell(triggersGcd: false);

      // GCD active but spell doesn't trigger GCD -> next condition wins.
      // With cooldownEndTick=100 and currentTick=50 the next condition is OnCooldown.
      bool ok = CombatValidator.TryValidate(
        caster, ValidId(1), spell,
        currentTick: 50, gcdEndTick: 100, cooldownEndTick: 100,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: false,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.OnCooldown, failReason,
        "When TriggersGcd is false, the GCD check must be skipped and the next rule (OnCooldown) decides.");
    }

    [Test]
    public void OnCooldown_WhenCurrentTickBeforeCooldownEnd() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", Vector3.zero);
      var spell  = MakeSpell();

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(1), spell,
        currentTick: 10, gcdEndTick: 0, cooldownEndTick: 25,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: false,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.OnCooldown, failReason);
    }

    [Test]
    public void NoTarget_WhenTargetIdInvalid() {
      var caster = NewTransform("caster", Vector3.zero);
      var spell  = MakeSpell();

      bool ok = CombatValidator.TryValidate(
        caster, default(NetworkId), spell,
        currentTick: 100, gcdEndTick: 0, cooldownEndTick: 0,
        isAlreadyCasting: false,
        targetTransform: null, isTargetDead: false,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.NoTarget, failReason);
    }

    [Test]
    public void TargetDead_WhenIsTargetDeadTrue() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", Vector3.zero);
      var spell  = MakeSpell();

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(7), spell,
        currentTick: 100, gcdEndTick: 0, cooldownEndTick: 0,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: true,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.TargetDead, failReason);
    }

    [Test]
    public void OutOfRange_WhenDistanceExceedsSpellRange() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", new Vector3(50f, 0f, 0f));
      var spell  = MakeSpell(range: 30f);

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(11), spell,
        currentTick: 100, gcdEndTick: 0, cooldownEndTick: 0,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: false,
        out var failReason);

      Assert.IsFalse(ok);
      Assert.AreEqual(CombatFailReason.OutOfRange, failReason);
    }

    [Test]
    public void Success_WhenAllChecksPass() {
      var caster = NewTransform("caster", Vector3.zero);
      var target = NewTransform("target", new Vector3(5f, 0f, 0f));
      var spell  = MakeSpell(range: 30f);

      bool ok = CombatValidator.TryValidate(
        caster, ValidId(13), spell,
        currentTick: 100, gcdEndTick: 0, cooldownEndTick: 0,
        isAlreadyCasting: false,
        targetTransform: target, isTargetDead: false,
        out var failReason);

      Assert.IsTrue(ok);
      Assert.AreEqual(CombatFailReason.None, failReason);
    }
  }
}
