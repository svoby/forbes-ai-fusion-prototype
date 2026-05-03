using NUnit.Framework;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Guards default cast-bar HUD constants (pure -- no Canvas). Layout itself is tweaked in Editor.
  /// </summary>
  [TestFixture]
  public sealed class CastBarLayoutDefaultsTests {
    [Test]
    public void DefaultHudConstants_FormFactorIsReasonable() {
      Assert.IsTrue(CastBarView.CastBarPanelWidth > 400f, "panel width");
      Assert.IsTrue(CastBarView.CastBarPanelHeight > 64f, "panel height");
      float lift = CastBarView.CastBarLiftFromBottomPx;
      Assert.IsTrue(lift >= 100f && lift <= 360f, $"lift px was {lift}");
    }

    [Test]
    public void HudLayoutVersion_Monotonic() {
      Assert.IsTrue(
        CastBarView.CurrentHudLayoutVersion >= 5,
        "When geometry changes bump version so EnsureForRunner can recreate HUD.");
    }
  }
}
