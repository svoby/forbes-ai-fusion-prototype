#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Writes a plain-text summary of the last Test Runner execution (Edit Mode or Play Mode)
/// started from the Unity Editor to <c>TestResults/last-editor-test-run.log</c> at repo root.
/// Batch builds may instead pass <c>-testResults</c> to Unity for XML output.
/// </summary>
[InitializeOnLoad]
internal static class ForbesTestRunFileLogger {
  static ForbesTestRunFileLogger() {
    var api = ScriptableObject.CreateInstance<TestRunnerApi>();
    api.RegisterCallbacks(new Callbacks());
  }

  sealed class Callbacks : ICallbacks {
    public void RunStarted(ITestAdaptor testsToRun) { }

    public void RunFinished(ITestResultAdaptor result) {
      WriteLog(result);
    }

    public void TestStarted(ITestAdaptor test) { }

    public void TestFinished(ITestResultAdaptor result) { }
  }

  static void WriteLog(ITestResultAdaptor root) {
    try {
      string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
      string dir = Path.Combine(projectRoot, "TestResults");
      Directory.CreateDirectory(dir);
      string path = Path.Combine(dir, "last-editor-test-run.log");

      var sb = new StringBuilder();
      sb.AppendLine($"Utc: {System.DateTime.UtcNow:O}");
      if (root != null) {
        sb.AppendLine($"Root: {root.FullName}");
        sb.AppendLine($"Root status: {root.TestStatus}");
        sb.AppendLine("--- Failures (leaf tests) ---");
        AppendLeafFailures(sb, root, "");
      } else {
        sb.AppendLine("(null root result)");
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
      Debug.Log($"[Forbes] Test run log written: {path}");
    } catch (System.Exception ex) {
      Debug.LogWarning($"[Forbes] Could not write test run log: {ex.Message}");
    }
  }

  static void AppendLeafFailures(StringBuilder sb, ITestResultAdaptor r, string indent) {
    if (r == null) {
      return;
    }

    var children = r.Children;
    var sawChild = false;
    if (children != null) {
      foreach (var child in children) {
        sawChild = true;
        AppendLeafFailures(sb, child, indent);
      }
    }

    if (sawChild) {
      return;
    }

    if (r.TestStatus != TestStatus.Failed) {
      return;
    }

    sb.AppendLine($"{indent}{r.FullName}");
    if (!string.IsNullOrEmpty(r.Message)) {
      sb.AppendLine($"{indent}  Message: {r.Message}");
    }

    if (!string.IsNullOrEmpty(r.StackTrace)) {
      foreach (var line in r.StackTrace.Split('\n')) {
        string trimmed = line.TrimEnd();
        if (trimmed.Length > 0) {
          sb.AppendLine($"{indent}  {trimmed}");
        }
      }
    }

    sb.AppendLine();
  }
}
#endif
