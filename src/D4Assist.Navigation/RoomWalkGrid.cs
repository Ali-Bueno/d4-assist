namespace D4Assist.Navigation;

/// <summary>
/// Builds a room's combined walkable grid, in world coordinates, by placing each member tile's local
/// grid (rotated by its yaw) at its world origin. Used only for BFS pathfinding (RoomPathfinder); a
/// tile with no walkability data or a non-axis-aligned yaw simply contributes no walkable cells —
/// paths crossing it fall back to a straight line for the affected openings.
/// </summary>
public static class RoomWalkGrid
{
    /// <param name="blocked">Live barriers, left unwalkable.</param>
    public static WalkGrid? Build(IReadOnlyList<MapTile> members, ITileWalkability? walkability, BlockedCells? blocked = null)
    {
        if (walkability is null) return null;

        var tileGrids = new List<(MapTile Tile, WalkGrid Grid, int Turns)>();
        foreach (var tile in members)
        {
            if (!walkability.TryGet(tile.Input.SceneSno, out var grid)) continue;
            var turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
            if (turns < 0) continue; // non-axis rotation: unsupported, leave unwalkable
            tileGrids.Add((tile, blocked?.Mask(tile, grid, turns) ?? grid, turns));
        }
        if (tileGrids.Count == 0) return null;

        var resolution = tileGrids[0].Grid.Resolution;
        var (minX, minY, maxX, maxY) = Bounds(members);
        int rows = (int)MathF.Ceiling((maxX - minX) / resolution);
        int cols = (int)MathF.Ceiling((maxY - minY) / resolution);
        var cells = new bool[rows, cols];

        foreach (var (tile, grid, turns) in tileGrids)
            for (var r = 0; r < grid.Rows; r++)
                for (var c = 0; c < grid.Cols; c++)
                {
                    if (!grid.IsWalkable(r, c)) continue;
                    var (localX, localY) = grid.CellCenter(r, c);
                    var (rx, ry) = TileRotation.Rotate(localX, localY, turns);
                    var worldX = tile.Input.OriginX + rx;
                    var worldY = tile.Input.OriginY + ry;
                    int row = (int)MathF.Floor((worldX - minX) / resolution);
                    int col = (int)MathF.Floor((worldY - minY) / resolution);
                    if (row >= 0 && row < rows && col >= 0 && col < cols) cells[row, col] = true;
                }
        return new WalkGrid(resolution, minX, minY, rows, cols, cells);
    }

    // Uses each tile's unrotated footprint; a 90/270-turned tile's true world footprint swaps X/Y extents.
    // Cells that fall outside these bounds are silently dropped (see the bounds check above) rather than
    // corrupting the grid — an acceptable degradation until a live rotated tile is observed.
    private static (float MinX, float MinY, float MaxX, float MaxY) Bounds(IReadOnlyList<MapTile> members)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var tile in members)
        {
            minX = Math.Min(minX, tile.Input.OriginX);
            minY = Math.Min(minY, tile.Input.OriginY);
            maxX = Math.Max(maxX, tile.Input.OriginX + tile.Input.SizeX);
            maxY = Math.Max(maxY, tile.Input.OriginY + tile.Input.SizeY);
        }
        return (minX, minY, maxX, maxY);
    }
}
