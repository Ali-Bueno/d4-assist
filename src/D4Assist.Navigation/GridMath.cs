namespace D4Assist.Navigation;

/// <summary>World-to-grid conversion. Tiles lie on a 32-unit grid (TileInput doc).</summary>
public static class GridMath
{
    public const float CellSize = 32f;

    /// <summary>The 32-unit grid cell index containing a world coordinate.</summary>
    public static int CellIndex(float worldCoordinate) => (int)MathF.Floor(worldCoordinate / CellSize);
}
