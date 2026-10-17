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

    private int[,]? _clearance;

    /// <summary>How many cells a walkable cell lies from the nearest blocked cell or the grid's edge (8-connected steps): 1
    /// beside a wall, 0 for a blocked cell or one off the grid. Computed once per grid.</summary>
    public int Clearance(int row, int col)
    {
        if (!IsWalkable(row, col)) return 0;
        _clearance ??= ComputeClearance();
        return _clearance[row, col];
    }

    // Multi-source BFS outward from every blocked cell and from the ring just off the grid.
    private int[,] ComputeClearance()
    {
        var distance = new int[Rows, Cols];
        var queue = new Queue<(int Row, int Col)>();
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                if (_cells[r, c] && HasBlockedNeighbour(r, c))
                {
                    distance[r, c] = 1;
                    queue.Enqueue((r, c));
                }
        while (queue.TryDequeue(out var cell))
            for (var dr = -1; dr <= 1; dr++)
                for (var dc = -1; dc <= 1; dc++)
                {
                    var (r, c) = (cell.Row + dr, cell.Col + dc);
                    if (!IsWalkable(r, c) || distance[r, c] != 0) continue;
                    distance[r, c] = distance[cell.Row, cell.Col] + 1;
                    queue.Enqueue((r, c));
                }
        return distance;
    }

    // Off the grid counts as blocked.
    private bool HasBlockedNeighbour(int row, int col)
    {
        for (var dr = -1; dr <= 1; dr++)
            for (var dc = -1; dc <= 1; dc++)
                if (!IsWalkable(row + dr, col + dc)) return true;
        return false;
    }

    /// <summary>The cell containing a coordinate, in the same frame as Origin.</summary>
    public (int Row, int Col) CellAt(float x, float y) =>
        ((int)MathF.Floor((x - OriginX) / Resolution), (int)MathF.Floor((y - OriginY) / Resolution));
}
