namespace D4Assist.Navigation;

/// <summary>
/// Live barriers the static walk map calls floor (a wall that stands until a monster dies), as 1-unit world cells:
/// cell (x, y) covers [x, x+1) x [y, y+1). A tile walk cell is blocked when its centre falls in one.
/// </summary>
public sealed class BlockedCells
{
    /// <summary>A blocked cell's side, in world units: the 1-unit radar grid the host lays actor footprints on (DoorObstacles).</summary>
    public const float CellSizeUnits = 1f;

    public static readonly BlockedCells None = new([]);

    private readonly HashSet<(int X, int Y)> _cells;

    public BlockedCells(IEnumerable<(int X, int Y)> cells) => _cells = [.. cells];

    public IReadOnlySet<(int X, int Y)> Cells => _cells;

    public bool IsEmpty => _cells.Count == 0;

    public bool Contains(float worldX, float worldY) =>
        _cells.Contains(((int)MathF.Floor(worldX / CellSizeUnits), (int)MathF.Floor(worldY / CellSizeUnits)));

    /// <summary>The tile's local walk grid without its blocked cells; null when no blocked cell lands on its floor.</summary>
    internal WalkGrid? Mask(MapTile tile, WalkGrid grid, int turns)
    {
        if (IsEmpty || !Overlaps(tile, grid, turns)) return null;
        var open = new bool[grid.Rows, grid.Cols];
        var changed = false;
        for (var r = 0; r < grid.Rows; r++)
            for (var c = 0; c < grid.Cols; c++)
            {
                if (!grid.IsWalkable(r, c)) continue;
                var (x, y) = TileRotation.ToWorld(tile.Input, grid.CellCenter(r, c), turns);
                if (Contains(x, y)) changed = true;
                else open[r, c] = true;
            }
        return changed ? new WalkGrid(grid.Resolution, grid.OriginX, grid.OriginY, grid.Rows, grid.Cols, open) : null;
    }

    // Any blocked cell inside the box the tile's turned grid covers: skips the cell-by-cell pass for every other tile.
    private bool Overlaps(MapTile tile, WalkGrid grid, int turns)
    {
        var a = TileRotation.ToWorld(tile.Input, (grid.OriginX, grid.OriginY), turns);
        var b = TileRotation.ToWorld(tile.Input, (grid.OriginX + grid.Rows * grid.Resolution, grid.OriginY + grid.Cols * grid.Resolution), turns);
        float minX = MathF.Min(a.X, b.X), maxX = MathF.Max(a.X, b.X), minY = MathF.Min(a.Y, b.Y), maxY = MathF.Max(a.Y, b.Y);
        return _cells.Any(c => (c.X + 1) * CellSizeUnits > minX && c.X * CellSizeUnits < maxX &&
                               (c.Y + 1) * CellSizeUnits > minY && c.Y * CellSizeUnits < maxY);
    }
}
