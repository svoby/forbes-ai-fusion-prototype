using System.Diagnostics;



/// <summary>

/// Central place for project logging. Prefer these helpers over raw <c>Debug.Log*</c> in gameplay code.

/// <list type="bullet">

///   <item><c>FORBES_LOG</c> — verbose channels: <see cref="Net"/>, <see cref="Health"/>, <see cref="Targeting"/> (Project Settings → Scripting Define Symbols).</item>

///   <item><see cref="Warn"/> / <see cref="Error"/> — always on (misconfiguration, actionable issues).</item>

///   <item><see cref="Diag"/> — optional always-on trace keyed by channel (use sparingly).</item>

/// </list>

/// </summary>

public static class ForbesLog {

  /// <summary>Optional always-on trace; throttle at call sites.</summary>

  public static void Diag(string channel, string message, UnityEngine.Object context = null) {

    UnityEngine.Debug.Log($"[ForbesDiag.{channel}] {message}", context);

  }



  /// <summary>Always-on warning (broken setup, missing references).</summary>

  public static void Warn(string message, UnityEngine.Object context = null) {

    UnityEngine.Debug.LogWarning(message, context);

  }



  /// <summary>Always-on error (broken setup).</summary>

  public static void Error(string message, UnityEngine.Object context = null) {

    UnityEngine.Debug.LogError(message, context);

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

