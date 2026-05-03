using System.Diagnostics;

/// <summary>
/// Project-wide log helpers gated by the <c>FORBES_LOG</c> Scripting Define Symbol
/// (Project Settings &gt; Player &gt; Other Settings &gt; Scripting Define Symbols).
/// All call sites compile out when the symbol is not defined.
/// </summary>
public static class ForbesLog {
  /// <summary>
  /// Always logs (not gated by <c>FORBES_LOG</c>). Use for one-off diagnostics and throttle
  /// at call sites — helpful when Editor scripting defines omit <c>FORBES_LOG</c> but Standalone has it.
  /// </summary>
  public static void Diag(string channel, string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log($"[ForbesDiag.{channel}] {message}", context);
  }

  [Conditional("FORBES_LOG")]
  public static void Net(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesNet] " + message, context);
  }

  [Conditional("FORBES_LOG")]
  public static void Health(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesHealth] " + message, context);
  }

  [Conditional("FORBES_LOG")]
  public static void Targeting(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesTargeting] " + message, context);
  }
}
