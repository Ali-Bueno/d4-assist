namespace D4Assist.Navigation;

/// <summary>
/// BFS over a room's combined walkable grid (8-connected, no cutting across a blocked corner), from
/// the player's cell to the nearest cell of a target opening. A room is at most ~64x64 units, i.e.
/// ~128x128 cells at the 1-unit map resolution — cheap to search fully every time (task spec).
/// </summary>
public static class RoomPathfinder
{
    private static readonly (int Dr, int Dc)[] Moves =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (-1, 1), (1, -1), (1, 1),
    };

    /// <summary>Radius, in grid cells, to search for a walkable cell when the player's own cell is
    /// blocked on the offline map (live log 2026-09-28: the dungeon entrance ladder and wall-hugging
    /// positions sit on cells the offline map marks unwalkable). Beyond this, treat the player as
    /// genuinely off-grid.</summary>
    public const int StartSnapRadiusCells = 3;

    /// <summary>How many line-of-sight samples per grid cell when checking a segment for
    /// <see cref="FarthestVisiblePoint"/>: fine enough that a single blocked cell is never skipped
    /// between samples.</summary>
    private const float LineOfSightSamplesPerCell = 4f;

    /// <summary>Finds the shortest cell path from <paramref name="fromWorld"/> to the nearest of
    /// <paramref name="targets"/>. False when unreachable (or off-grid) — caller falls back to a straight line.</summary>
    public static bool TryFindPath(WalkGrid grid, (float X, float Y) fromWorld, IReadOnlyList<(int Row, int Col)> targets,
        out List<(float X, float Y)> path, out float length)
    {
        path = [];
        length = 0f;
        var start = grid.CellAt(fromWorld.X, fromWorld.Y);
        if (!grid.IsWalkable(start.Row, start.Col) && !TrySnapToWalkable(grid, fromWorld, start, out start)) return false;

        var targetSet = new HashSet<(int, int)>(targets);
        if (targetSet.Count == 0) return false;

        var previous = new Dictionary<(int, int), (int, int)>();
        var visited = new HashSet<(int, int)> { start };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);
        (int, int)? found = targetSet.Contains(start) ? start : null;

        while (found is null && queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (dr, dc) in Moves)
            {
                var next = (current.Item1 + dr, current.Item2 + dc);
                if (visited.Contains(next) || !grid.IsWalkable(next.Item1, next.Item2)) continue;
                if (dr != 0 && dc != 0 &&
                    (!grid.IsWalkable(current.Item1 + dr, current.Item2) || !grid.IsWalkable(current.Item1, current.Item2 + dc)))
                    continue; // no corner-cutting: both orthogonal neighbours of a diagonal step must be open

                visited.Add(next);
                previous[next] = current;
                if (targetSet.Contains(next)) { found = next; break; }
                queue.Enqueue(next);
            }
        }
        if (found is not { } end) return false;

        var cells = new List<(int, int)> { end };
        var cursor = end;
        while (previous.TryGetValue(cursor, out var prev)) { cells.Add(prev); cursor = prev; }
        cells.Reverse();

        path = cells.Select(c => grid.CellCenter(c.Item1, c.Item2)).ToList();
        for (var i = 1; i < path.Count; i++)
            length += Distance(path[i - 1], path[i]);
        return true;
    }

    /// <summary>The farthest path point still in a straight line of sight from the path's start —
    /// the route's real next heading, instead of a fixed look-ahead distance that lands on whichever
    /// grid-aligned direction the path happens to start in (live log 2026-09-28: BFS cell paths start
    /// along a grid axis, so a short fixed look-ahead read as a screen diagonal almost every time).
    /// Scans from the end back toward index 1 and returns the first point with clear sight of
    /// <c>path[0]</c>; falls back to <c>path[1]</c> if nothing beyond it is visible, or <c>path[0]</c>
    /// itself for a single-point path.</summary>
    public static (float X, float Y) FarthestVisiblePoint(WalkGrid grid, List<(float X, float Y)> path)
    {
        if (path.Count <= 1) return path.Count == 1 ? path[0] : (0f, 0f);

        for (var i = path.Count - 1; i >= 1; i--)
            if (HasLineOfSight(grid, path[0], path[i])) return path[i];

        return path[1];
    }

    // Every sampled cell along the segment must be walkable; a diagonal cut around a corner samples
    // into the blocked interior and correctly fails even though the two endpoints alone would not catch it.
    private static bool HasLineOfSight(WalkGrid grid, (float X, float Y) from, (float X, float Y) to)
    {
        var samples = Math.Max(1, (int)MathF.Ceiling(Distance(from, to) / (grid.Resolution / LineOfSightSamplesPerCell)));
        for (var s = 0; s <= samples; s++)
        {
            var t = s / (float)samples;
            var cell = grid.CellAt(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
            if (!grid.IsWalkable(cell.Row, cell.Col)) return false;
        }
        return true;
    }

    private static bool TrySnapToWalkable(WalkGrid grid, (float X, float Y) fromWorld, (int Row, int Col) start, out (int Row, int Col) snapped)
    {
        snapped = start;
        var bestDistance = float.MaxValue;
        var maxDistance = StartSnapRadiusCells * grid.Resolution;
        for (var dr = -StartSnapRadiusCells; dr <= StartSnapRadiusCells; dr++)
            for (var dc = -StartSnapRadiusCells; dc <= StartSnapRadiusCells; dc++)
            {
                var candidate = (start.Row + dr, start.Col + dc);
                if (!grid.IsWalkable(candidate.Item1, candidate.Item2)) continue;
                var distance = Distance(fromWorld, grid.CellCenter(candidate.Item1, candidate.Item2));
                if (distance > maxDistance || distance >= bestDistance) continue;
                bestDistance = distance;
                snapped = candidate;
            }
        return bestDistance < float.MaxValue;
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) =>
        MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2));
}
