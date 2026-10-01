namespace D4Assist.Navigation;

/// <summary>
/// A tile's nodes in the <see cref="TileGraph"/>: one for the whole tile, or, when live barriers (<see cref="BlockedCells"/>)
/// take floor from its walk map, one per 4-connected floor region left (the same reach as RoomPathfinder's no-corner-cutting steps).
/// </summary>
internal sealed class TileRegions
{
    private static readonly (int Dr, int Dc)[] Steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    private readonly WalkGrid? _static;
    private readonly WalkGrid? _open;
    private readonly int[,]? _labels; // 1-based region per open cell, 0 elsewhere
    private readonly int _turns;

    private TileRegions(MapTile tile, int regions, WalkGrid? staticGrid = null, WalkGrid? open = null, int[,]? labels = null, int turns = 0)
    {
        Tile = tile;
        Nodes = [.. Enumerable.Range(0, Math.Max(1, regions)).Select(_ => new TileNode(tile))];
        _static = staticGrid;
        _open = open;
        _labels = labels;
        _turns = turns;
    }

    public MapTile Tile { get; }

    /// <summary>One node per region; a tile left with no floor keeps one node that nothing joins.</summary>
    public IReadOnlyList<TileNode> Nodes { get; }

    public bool IsSplit => _labels is not null;

    public static TileRegions Build(MapTile tile, ITileWalkability? walkability, BlockedCells blocked)
    {
        if (blocked.IsEmpty || walkability is null || !walkability.TryGet(tile.Input.SceneSno, out var grid)) return new(tile, 1);
        var turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
        if (turns < 0 || blocked.Mask(tile, grid, turns) is not { } open) return new(tile, 1);
        var (labels, count) = Label(open);
        return new(tile, count, grid, open, labels, turns);
    }

    /// <summary>The region holding the point's cell, else the one nearest to it (an objective on a wall, the player hugging a barrier).</summary>
    public TileNode At((float X, float Y) point)
    {
        if (_labels is null) return Nodes[0];
        var local = TileRotation.ToLocal(Tile.Input, point, _turns);
        var (row, col) = _open!.CellAt(local.X, local.Y);
        if (RegionAt(row, col) is { } region) return Nodes[region];

        var best = (Region: 0, Distance: float.MaxValue);
        for (var r = 0; r < _open.Rows; r++)
            for (var c = 0; c < _open.Cols; c++)
            {
                if (RegionAt(r, c) is not { } candidate) continue;
                var distance = TileRoutes.Distance(local, _open.CellCenter(r, c));
                if (distance < best.Distance) best = (candidate, distance);
            }
        return Nodes[best.Region];
    }

    /// <summary>Per point, the region of its floor cell (null when blocked or not floor); when none of the points is floor on the
    /// static map either (a walk map that doesn't reach that edge), every point gets the region nearest to <paramref name="fallback"/>.</summary>
    public TileNode?[] Along(IReadOnlyList<(float X, float Y)> points, (float X, float Y) fallback)
    {
        if (_labels is null) return [.. points.Select(_ => Nodes[0])];
        var cells = points.Select(p => TileRotation.ToLocal(Tile.Input, p, _turns)).Select(l => _open!.CellAt(l.X, l.Y)).ToList();
        if (!cells.Any(c => _static!.IsWalkable(c.Row, c.Col)))
        {
            var nearest = At(fallback);
            return [.. points.Select(_ => nearest)];
        }
        return [.. cells.Select(c => RegionAt(c.Row, c.Col) is { } region ? Nodes[region] : null)];
    }

    private int? RegionAt(int row, int col) => _open!.IsWalkable(row, col) ? _labels![row, col] - 1 : null;

    private static (int[,] Labels, int Count) Label(WalkGrid open)
    {
        var labels = new int[open.Rows, open.Cols];
        var count = 0;
        var queue = new Queue<(int Row, int Col)>();
        for (var r = 0; r < open.Rows; r++)
            for (var c = 0; c < open.Cols; c++)
            {
                if (!open.IsWalkable(r, c) || labels[r, c] != 0) continue;
                labels[r, c] = ++count;
                queue.Enqueue((r, c));
                while (queue.TryDequeue(out var cell))
                    foreach (var (dr, dc) in Steps)
                    {
                        int nr = cell.Row + dr, nc = cell.Col + dc;
                        if (!open.IsWalkable(nr, nc) || labels[nr, nc] != 0) continue;
                        labels[nr, nc] = count;
                        queue.Enqueue((nr, nc));
                    }
            }
        return (labels, count);
    }
}
