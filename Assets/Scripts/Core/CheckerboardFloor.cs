using UnityEngine;

/// <summary>
/// Builds a 5 × 5 black/white checkerboard floor at startup if one does not already exist.
/// The editor setup tool (Tools → Fusion → Scene → Apply Full Combat Setup) also
/// calls <see cref="Create"/> so the floor is saved in the scene asset.
/// </summary>
public static class CheckerboardFloor {
  public const string ParentName = "Floor";

  /// <summary>Edge length of the square tile grid (total tiles = this squared).</summary>
  public const int GridDimension = 5;

  const float TileSize   = 10f;
  const float TileHeight = 0.2f;

  static readonly Color ColorA = Color.white;
  static readonly Color ColorB = Color.black;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void AutoCreate() {
    var existing = GameObject.Find(ParentName);

    // If there is a legacy single-object floor (no children = old Plane/Cube),
    // destroy it so we can replace it with the checkerboard.
    if (existing != null && existing.transform.childCount == 0) {
      Object.Destroy(existing);
      existing = null;
    }

    int expectedTiles = GridDimension * GridDimension;
    if (existing != null && existing.transform.childCount == expectedTiles) return;

    if (existing != null) Object.Destroy(existing);

    Create();
  }

  /// <summary>Creates the 5 × 5 checkerboard floor centred at the world origin.</summary>
  public static void Create() {
    var parent = new GameObject(ParentName);
    // Do NOT set isStatic = true on runtime-created objects: Unity does not rebuild
    // the static physics broadphase at runtime, so statically-flagged objects won't
    // be found by Physics.Raycast / OverlapSphere.

    var matA = BuildMat(ColorA);
    var matB = BuildMat(ColorB);

    float halfSpan = (GridDimension - 1) * TileSize * 0.5f;

    for (int row = 0; row < GridDimension; row++) {
      for (int col = 0; col < GridDimension; col++) {
        var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = $"Tile_{row}_{col}";
        tile.transform.SetParent(parent.transform, false);
        tile.transform.localPosition = new Vector3(
          col * TileSize - halfSpan,
          -TileHeight * 0.5f,
          row * TileSize - halfSpan
        );
        tile.transform.localScale = new Vector3(TileSize, TileHeight, TileSize);
        tile.GetComponent<MeshRenderer>().sharedMaterial =
          ((row + col) % 2 == 0) ? matA : matB;
      }
    }
  }

  static Material BuildMat(Color c) {
    var shader = Shader.Find("Universal Render Pipeline/Lit")
              ?? Shader.Find("Standard")
              ?? Shader.Find("Sprites/Default");
    return new Material(shader) { color = c };
  }
}
