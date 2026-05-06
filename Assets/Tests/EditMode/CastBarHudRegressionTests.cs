using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Guards the cast bar / HUD wiring contracts: single runtime entry point, bundled font, no editor auto-bootstrap regressions.
  /// </summary>
  [TestFixture]
  public sealed class CastBarHudRegressionTests {
    static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    static string ReadUtf8(string relativeToProject) {
      return File.ReadAllText(Path.Combine(ProjectRoot(), relativeToProject));
    }

    [Test]
    public void BundledNotoTtf_ExistsOnDisk() {
      string p = Path.Combine(ProjectRoot(), "Assets", "Resources", "ForbesHud", "NotoSans-Regular.ttf");
      Assert.IsTrue(File.Exists(p), $"Expected font at {p}");
    }

    [Test]
    public void BundledHudFont_ResourcesLoads() {
      var f = Resources.Load<Font>(CastBarView.BundledHudFontResourcesPath);
      Assert.IsNotNull(f, "Resources.Load must resolve the shipped NotoSans .ttf Font asset.");
    }

    [Test]
    public void ResolveDefaultHudFont_IsNotNull_WithBundledAssets() {
      var f = CastBarView.ResolveDefaultHudFont();
      Assert.IsNotNull(f, $"{nameof(CastBarView.ResolveDefaultHudFont)} should prefer bundled NotoSans when present.");
    }

    [Test]
    public void EnsureForRunner_ReturnsNullWhenRunnerNull() {
      Assert.IsNull(RuntimeHudBootstrap.EnsureForRunner(null));
    }

    [Test]
    public void CombatFeedbackBannerView_DoesNotCallEnsureForRunner_DuplicateProvisioningRegresses() {
      string src = ReadUtf8(Path.Combine("Assets", "Scripts", "UI", "CombatFeedbackBannerView.cs"));
      StringAssert.DoesNotContain("EnsureForRunner", src, "Runtime HUD must be created only from FusionHudToggle (one coroutine), not CombatFeedbackBannerView.");
    }

    [Test]
    public void CastBarView_DoesNotOwnRuntimeHudCanvas() {
      string src = ReadUtf8(Path.Combine("Assets", "Scripts", "UI", "CastBarView.cs"));
      StringAssert.DoesNotContain("ForbesHudCanvas", src, "CastBarView should only present casting state, not own the HUD canvas.");
      StringAssert.DoesNotContain("EnsureForRunner", src, "CastBarView should not provision the runtime HUD.");
    }

    [Test]
    public void FusionHudToggle_InvokesEnsureForRunnerOnce() {
      string src = ReadUtf8(Path.Combine("Assets", "Scripts", "UI", "FusionHudToggle.cs"));
      int n = Regex.Matches(src, @"\bRuntimeHudBootstrap\.EnsureForRunner\s*\(").Count;
      Assert.AreEqual(1, n, "Single post-yield call keeps one Destroy/Create ordering; duplicate sites race.");
    }

    [Test]
    public void RuntimeHudBootstrap_EnsureForRunner_InvokedOnlyFromFusionHudToggleInScriptsTree() {
      string scripts = Path.Combine(ProjectRoot(), "Assets", "Scripts");
      var rx = new Regex(@"\bRuntimeHudBootstrap\.EnsureForRunner\s*\(", RegexOptions.Compiled);
      foreach (var file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories)) {
        string rel = file.Substring(ProjectRoot().Length).Replace('\\', '/');
        if (rel.EndsWith("/RuntimeHudBootstrap.cs")) {
          continue;
        }

        string text = File.ReadAllText(file);
        if (!rx.IsMatch(text)) {
          continue;
        }

        Assert.IsTrue(
          rel.Replace('\\', '/').EndsWith("/UI/FusionHudToggle.cs"),
          $"RuntimeHudBootstrap.EnsureForRunner must stay on FusionHudToggle only (found in {rel}).");
      }
    }

    [Test]
    public void EditorSceneSetup_HasNoCastBarAutoReloadBootstrap() {
      string path = Path.Combine("Assets", "Editor", "ForbesFusionSharedSceneSetup.cs");
      string src = ReadUtf8(path);
      StringAssert.DoesNotContain("ForbesCastBarHierarchyBootstrap", src);
      StringAssert.DoesNotContain("[InitializeOnLoad]", src);
      StringAssert.DoesNotContain("sceneOpened", src);
      StringAssert.DoesNotContain("EnsureCastBarHudAllRunnersInEditMode", src);
      StringAssert.DoesNotContain("Resources.FindObjectsOfTypeAll<NetworkRunner>", src);
      StringAssert.Contains("EnsureCastBarHudForLoadedScenes", src, "Menu-only helper must stay manual.");
    }
  }
}
