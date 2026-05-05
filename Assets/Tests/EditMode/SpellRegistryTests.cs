using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Pins every <see cref="SpellRegistry"/> entry plus the bounds of <see cref="SpellRegistry.Get"/>.
  /// Constants here are load-bearing per docs/TEST_COVERAGE_PLAN.md section H rule 6.
  /// </summary>
  [TestFixture]
  public class SpellRegistryTests {
    [TestCase((byte)0)]
    [TestCase((byte)4)]
    [TestCase((byte)5)]
    [TestCase((byte)127)]
    [TestCase((byte)255)]
    public void Get_OutOfRangeId_ReturnsInvalidSpell(byte id) {
      var spell = SpellRegistry.Get(id);
      Assert.IsFalse(spell.IsValid, $"Expected SpellRegistry.Get({id}) to return an invalid SpellData.");
    }

    [Test]
    public void Get_Id1_ReturnsFireball() {
      var spell = SpellRegistry.Get(1);
      Assert.IsTrue(spell.IsValid);
      Assert.AreEqual("Fireball", spell.Name);
      Assert.AreEqual(2.5f, spell.CastTimeSec);
      Assert.AreEqual(0f,   spell.CooldownSec);
      Assert.AreEqual(30f,  spell.RangeMeters);
      Assert.AreEqual(30f,  spell.Damage);
      Assert.IsTrue(spell.TriggersGcd);
      Assert.Greater(spell.ProjectileSpeedMetersPerSecond, 0f,
        "Fireball carries a logical projectile for travel-time gameplay.");
    }

    [Test]
    public void Get_Id2_ReturnsArcaneShot() {
      var spell = SpellRegistry.Get(2);
      Assert.IsTrue(spell.IsValid);
      Assert.AreEqual("Arcane Shot", spell.Name);
      Assert.AreEqual(0f,   spell.CastTimeSec);
      Assert.AreEqual(3f,   spell.CooldownSec);
      Assert.AreEqual(25f,  spell.RangeMeters);
      Assert.AreEqual(15f,  spell.Damage);
      Assert.IsTrue(spell.TriggersGcd);
      Assert.AreEqual(0f, spell.ProjectileSpeedMetersPerSecond,
        "Arcane Shot remains instant / hitscan (speed convention 0).");
      Assert.IsFalse(SpellTravelLogic.HasProjectile(spell));
    }

    [Test]
    public void Get_Id3_ReturnsHeavyBlast() {
      var spell = SpellRegistry.Get(3);
      Assert.IsTrue(spell.IsValid);
      Assert.AreEqual("Heavy Blast", spell.Name);
      Assert.AreEqual(2.5f, spell.CastTimeSec);
      Assert.AreEqual(4f,   spell.CooldownSec);
      Assert.AreEqual(30f,  spell.RangeMeters);
      Assert.AreEqual(60f,  spell.Damage);
      Assert.IsTrue(spell.TriggersGcd);
      Assert.AreEqual(0f, spell.ProjectileSpeedMetersPerSecond);
    }

    [Test]
    public void All_TableIsContiguous_AndHasThreeEntries() {
      Assert.AreEqual(3, SpellRegistry.All.Length, "SpellRegistry.All length drifted from the documented 3 entries.");
      for (int i = 0; i < SpellRegistry.All.Length; i++) {
        Assert.AreEqual((byte)(i + 1), SpellRegistry.All[i].Id,
          $"SpellRegistry.All[{i}].Id must equal {i + 1} (1-based contiguous indexing).");
      }
    }
  }
}
