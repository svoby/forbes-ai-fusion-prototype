using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Verifies the runner-missing diagnostic in <see cref="ActiveSpellInstancePresenter"/>:
  /// a warning is emitted at most once when no NetworkRunner is found after Start.
  /// </summary>
  public class ActiveSpellInstancePresenterDiagnosticTests {
    static readonly MethodInfo LateUpdateMethod =
      typeof(ActiveSpellInstancePresenter).GetMethod(
        "LateUpdate",
        BindingFlags.NonPublic | BindingFlags.Instance);

    static void CallLateUpdate(ActiveSpellInstancePresenter presenter) =>
      LateUpdateMethod.Invoke(presenter, null);

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

      CallLateUpdate(presenter); // first call — warning expected here
      CallLateUpdate(presenter); // second call — no additional warning
      // LogAssert fails the test if the expected warning was never logged,
      // and also fails if an unexpected warning is logged.
    }

    [Test]
    public void LateUpdate_WhenNoRunner_DoesNotLogOnSubsequentCalls() {
      var presenter = _go.AddComponent<ActiveSpellInstancePresenter>();

      // Consume the single expected warning from the first call.
      LogAssert.Expect(LogType.Warning,
        new System.Text.RegularExpressions.Regex(@"\[ActiveSpellInstancePresenter\].*No NetworkRunner"));
      CallLateUpdate(presenter);

      // Subsequent calls must not emit any warning.
      LogAssert.NoUnexpectedReceived();
      CallLateUpdate(presenter);
      CallLateUpdate(presenter);
      LogAssert.NoUnexpectedReceived();
    }
  }
}
