using System.Diagnostics;

/// <summary>
/// Project-wide log helpers gated by the <c>FORBES_LOG</c> Scripting Define Symbol
/// (Project Settings &gt; Player &gt; Other Settings &gt; Scripting Define Symbols).
/// All call sites compile out when the symbol is not defined.
/// </summary>
public static class ForbesLog {
  [Conditional("FORBES_LOG")]
  public static void Net(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesNet] " + message, context);
  }

  [Conditional("FORBES_LOG")]
  public static void Health(string message, UnityEngine.Object context = null) {
    UnityEngine.Debug.Log("[ForbesHealth] " + message, context);
  }
}
