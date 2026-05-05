using UnityEngine;

/// <summary>
/// Builds a 5 × 5 checkerboard floor at startup if one does not already exist (or the saved layout is stale).
/// The editor setup tool (Tools → Fusion → Scene → Apply Full Combat Setup) also
/// calls <see cref="Create"/> so the floor is saved in the scene asset.
/// </summary>
public static class CheckerboardFloor {
  public const string ParentName = "Floor";

  public const int GridSize = 5;
  const float TileSize   = 10f;
  const float TileHeight = 0.2f;

  static readonly Color ColorA = Color.white;
  static readonly Color ColorB = Color.black;

  static int ExpectedTileCount => GridSize * GridSize;

  /// <summary>True when <paramref name="floor"/> is the current checkerboard (tile count matches grid).</summary>
  public static bool MatchesCurrentGrid(GameObject floor) {
    return floor != null && floor.transform.childCount == ExpectedTileCount;
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void AutoCreate() {
    var existing = GameObject.Find(ParentName);

    if (existing != null) {
      if (MatchesCurrentGrid(existing)) return;
      Object.Destroy(existing);
    }

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

    float halfSpan = (GridSize - 1) * TileSize * 0.5f;

    for (int row = 0; row < GridSize; row++) {
      for (int col = 0; col < GridSize; col++) {
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
