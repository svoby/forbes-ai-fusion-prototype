using UnityEngine;

/// <summary>
/// Shared cosmetic colours for spell-related primitives (fireball trail, impact flash).
/// Builds materials from URP/Unlit (<see cref="UnlitOrbBaseResourcePath"/>) so primitives
/// are not tied to Unity's procedural default material — that default can vary by platform
/// and appear magenta on some clients while lit correctly in the Editor.
/// </summary>
public static class SpellVisualColors {
  /// <remarks>Loads from <see cref="Resources"/> (no extension).</remarks>
  public const string UnlitOrbBaseResourcePath = "Cosmetics/SpellPrimitive_UnlitBase";

  public static readonly Color Fireball = new Color(1f, 0.45f, 0.05f);

  static Material _cachedUnlitOrbTemplate;

  /// <summary>URP/Unlit orb material tinted for one use (assign to <see cref="Renderer.material"/>).</summary>
  public static Material NewUnlitOrbMaterial(Color tint) {
    Material mat = InstantiateUnlitOrbBase();
    ApplyOrbTint(mat, tint);
    return mat;
  }

  /// <inheritdoc cref="NewUnlitOrbMaterial(Color)"/>
  public static Material NewFireballOrbMaterial() => NewUnlitOrbMaterial(Fireball);

  static Material InstantiateUnlitOrbBase() {
    if (_cachedUnlitOrbTemplate == null) {
      _cachedUnlitOrbTemplate = Resources.Load<Material>(UnlitOrbBaseResourcePath);
    }

    if (_cachedUnlitOrbTemplate != null) {
      return new Material(_cachedUnlitOrbTemplate);
    }

    var shader =
      Shader.Find("Universal Render Pipeline/Unlit")
      ?? Shader.Find("Universal Render Pipeline/Lit");
    if (shader == null) {
      throw new System.InvalidOperationException(
        "[SpellVisualColors] URP not available (Unlit/Lit shaders missing). Cannot create spell orb material."
      );
    }

    Debug.LogWarning(
      "[SpellVisualColors] Resources material \"" + UnlitOrbBaseResourcePath +
      "\" missing — using Shader.Find fallback (prefer fixing the asset path)."
    );
    return new Material(shader);
  }

  static void ApplyOrbTint(Material material, Color tint) {
    if (material == null) {
      return;
    }

    material.color = tint;
    if (material.HasProperty("_BaseColor")) {
      material.SetColor("_BaseColor", tint);
    }

    if (material.HasProperty("_Color")) {
      material.SetColor("_Color", tint);
    }
  }
}
