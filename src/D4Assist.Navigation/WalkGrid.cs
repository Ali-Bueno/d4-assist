namespace D4Assist.Navigation;

/// <summary>
/// A 2D walkable-cell grid, indexed [row, col] with row varying along the grid's first axis and
/// col along its second. Used both for a single tile's local walkability (from
/// <see cref="ITileWalkability"/>, row = local X, col = local Y — verified in
/// research/dungeon-data.md §4.1 and against the live bug tile, see WalkGridTests) and for a room's
/// combined world-space grid built by <see cref="RoomWalkGrid"/> (row/col there index world X/Y).
/// </summary>
public sealed class WalkGrid
{
    public float Resolution { get; }
    public float OriginX { get; }
    public float OriginY { get; }
    public int Rows { get; }
    public int Cols { get; }

    private readonly bool[,] _cells;

    public WalkGrid(float resolution, float originX, float originY, int rows, int cols, bool[,] cells)
    {
        Resolution = resolution;
        OriginX = originX;
        OriginY = originY;
        Rows = rows;
        Cols = cols;
        _cells = cells;
    }

    public bool IsWalkable(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols && _cells[row, col];

    /// <summary>World/local coordinate of a cell's center, in whatever frame Origin is expressed in.</summary>
    public (float X, float Y) CellCenter(int row, int col) =>
        (OriginX + (row + 0.5f) * Resolution, OriginY + (col + 0.5f) * Resolution);

    /// <summary>The cell containing a coordinate, in the same frame as Origin.</summary>
    public (int Row, int Col) CellAt(float x, float y) =>
        ((int)MathF.Floor((x - OriginX) / Resolution), (int)MathF.Floor((y - OriginY) / Resolution));
}
