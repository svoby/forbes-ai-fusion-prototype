using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Verifies the runner-missing diagnostic in <see cref="ActiveSpellInstancePresenter"/>:
  /// a warning is emitted at most once when no NetworkRunner is found, but the lazy
  /// lookup continues each frame so the presenter recovers if a runner appears later.
  /// </summary>
  public class ActiveSpellInstancePresenterDiagnosticTests {
    static readonly MethodInfo LateUpdateMethod =
      typeof(ActiveSpellInstancePresenter).GetMethod(
        "LateUpdate",
        BindingFlags.NonPublic | BindingFlags.Instance);

    static readonly FieldInfo RunnerField =
      typeof(ActiveSpellInstancePresenter).GetField(
        "_runner",
        BindingFlags.NonPublic | BindingFlags.Instance);

    static void CallLateUpdate(ActiveSpellInstancePresenter presenter) =>
      LateUpdateMethod.Invoke(presenter, null);

    static void InjectRunner(ActiveSpellInstancePresenter presenter, NetworkRunner runner) =>
      RunnerField.SetValue(presenter, runner);

    GameObject _go;

    [SetUp]
    public void SetUp() {
      _go = new GameObject("PresenterHost");
      _go.AddComponent<ActiveSpellInstanceRegistry>();
    }

    [TearDown]
    public void TearDown() {
      Object.DestroyImmediate(_go);
    }

    [Test]
    public void LateUpdate_WhenNoRunner_LogsWarningExactlyOnce() {
      var presenter = _go.AddComponent<ActiveSpellInstancePresenter>();

      LogAssert.Expect(LogType.Warning,
        new System.Text.RegularExpressions.Regex(@"\[ActiveSpellInstancePresenter\].*No NetworkRunner"));

      CallLateUpdate(presenter); // first call — warning expected
      CallLateUpdate(presenter); // second call — no additional warning
      // LogAssert fails if the expected warning was never logged,
      // and also fails if an unexpected warning is logged.
    }

    [Test]
    public void LateUpdate_WhenNoRunner_DoesNotLogOnSubsequentCalls() {
      var presenter = _go.AddComponent<ActiveSpellInstancePresenter>();

      LogAssert.Expect(LogType.Warning,
        new System.Text.RegularExpressions.Regex(@"\[ActiveSpellInstancePresenter\].*No NetworkRunner"));
      CallLateUpdate(presenter);

      // Subsequent calls must not emit any warning.
      LogAssert.NoUnexpectedReceived();
      CallLateUpdate(presenter);
      CallLateUpdate(presenter);
      LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void LateUpdate_AfterWarning_RecoversSilentlyWhenRunnerAppearsLater() {
      var presenter = _go.AddComponent<ActiveSpellInstancePresenter>();

      // First call with no runner — emits the one-time warning.
      LogAssert.Expect(LogType.Warning,
        new System.Text.RegularExpressions.Regex(@"\[ActiveSpellInstancePresenter\].*No NetworkRunner"));
      CallLateUpdate(presenter);

      // Simulate a runner becoming available by injecting it directly.
      // (FindFirstObjectByType would find it in a real scene; here we bypass
      //  the scene scan to keep the test self-contained.)
      var runnerGo = new GameObject("FakeRunner");
      var runner   = runnerGo.AddComponent<NetworkRunner>();
      InjectRunner(presenter, runner);

      // Subsequent calls must not emit any warning and must not be blocked.
      LogAssert.NoUnexpectedReceived();
      CallLateUpdate(presenter);
      LogAssert.NoUnexpectedReceived();

      Object.DestroyImmediate(runnerGo);
    }
  }
}
