namespace D4Assist.Navigation;

/// <summary>
/// Shortest-path search over a room's combined walkable grid (8-connected, no cutting across a blocked corner), from
/// the player's cell to the nearest cell of a target opening. A room is at most ~64x64 units, i.e.
/// ~128x128 cells at the 1-unit map resolution — cheap to search fully every time (task spec).
/// </summary>
/// <remarks>A step beside a wall costs more (WallPenalty), so a route runs down the middle of a walkway rather than along its
/// edge: the shortest path hugged the edge of the Skovos docks' pier, its bearing aimed the player along that edge, and they
/// caught on a post the edge passes, the bearing barely turning (live 2026-10-17).</remarks>
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

    /// <summary>How far the path a bearing shortcuts may stray from the bearing's line. The path keeps off the walls, so a
    /// bearing that follows it does too: from beside a wall it first aims toward the middle.</summary>
    public const float BearingSlackUnits = 1.5f;

    // The extra cost, per unit walked, of a step onto a cell 1 or 2 cells from a wall (none from 3 on).
    private static float WallPenalty(int clearance) => clearance switch { 1 => 4f, 2 => 1f, _ => 0f };

    /// <summary>How near a jump point the floor must be to use it, and to land from it: jump points stand on the edge of their
    /// floor, often on a cell the walk map leaves out (Philios mindscape, 2026-10-18).</summary>
    public const float JumpReachUnits = 2.5f;

    // Using a jump costs this much more than its length: walking around is preferred when it is about as short.
    private const float JumpExtraCost = 4f;

    /// <summary>Finds the shortest cell path from <paramref name="fromWorld"/> to the nearest of
    /// <paramref name="targets"/>. False when unreachable (or off-grid) — caller falls back to a straight line.</summary>
    public static bool TryFindPath(WalkGrid grid, (float X, float Y) fromWorld, IReadOnlyList<(int Row, int Col)> targets,
        out List<(float X, float Y)> path, out float length) =>
        TryFindPath(grid, fromWorld, targets, [], out path, out length, out _);

    /// <summary>As the plain search, but <paramref name="jumps"/> join the floor near one jump point to the floor near the other.
    /// <paramref name="firstJump"/> is the index in <paramref name="path"/> of the cell the path's first jump starts from, and
    /// the jump point used there; null when the path needs no jump.</summary>
    public static bool TryFindPath(WalkGrid grid, (float X, float Y) fromWorld, IReadOnlyList<(int Row, int Col)> targets,
        IReadOnlyList<JumpLink> jumps, out List<(float X, float Y)> path, out float length, out (int Index, (float X, float Y) At)? firstJump)
    {
        path = [];
        length = 0f;
        firstJump = null;
        var start = grid.CellAt(fromWorld.X, fromWorld.Y);
        if (!grid.IsWalkable(start.Row, start.Col) && !TrySnapToWalkable(grid, fromWorld, start, out start)) return false;

        var targetSet = new HashSet<(int, int)>(targets);
        if (targetSet.Count == 0) return false;

        var takeoffs = Takeoffs(grid, jumps);
        var jumpedFrom = new Dictionary<(int, int), (float X, float Y)>(); // landing cell -> the jump point used to reach it
        var previous = new Dictionary<(int, int), (int, int)>();
        var cost = new Dictionary<(int, int), float> { [start] = 0f };
        var done = new HashSet<(int, int)>();
        var queue = new PriorityQueue<(int, int), float>();
        queue.Enqueue(start, 0f);
        (int, int)? found = null;

        while (queue.TryDequeue(out var current, out var soFar))
        {
            if (!done.Add(current)) continue;
            if (targetSet.Contains(current)) { found = current; break; }
            foreach (var (dr, dc) in Moves)
            {
                var next = (current.Item1 + dr, current.Item2 + dc);
                if (done.Contains(next) || !grid.IsWalkable(next.Item1, next.Item2)) continue;
                if (dr != 0 && dc != 0 &&
                    (!grid.IsWalkable(current.Item1 + dr, current.Item2) || !grid.IsWalkable(current.Item1, current.Item2 + dc)))
                    continue; // no corner-cutting: both orthogonal neighbours of a diagonal step must be open

                var step = (dr != 0 && dc != 0 ? MathF.Sqrt(2f) : 1f) * (1f + WallPenalty(grid.Clearance(next.Item1, next.Item2)));
                if (cost.TryGetValue(next, out var known) && known <= soFar + step) continue;
                cost[next] = soFar + step;
                previous[next] = current;
                jumpedFrom.Remove(next);
                queue.Enqueue(next, soFar + step);
            }
            if (!takeoffs.TryGetValue(current, out var leaps)) continue;
            foreach (var (landing, leapCost, at) in leaps)
            {
                if (done.Contains(landing) || (cost.TryGetValue(landing, out var known) && known <= soFar + leapCost)) continue;
                cost[landing] = soFar + leapCost;
                previous[landing] = current;
                jumpedFrom[landing] = at;
                queue.Enqueue(landing, soFar + leapCost);
            }
        }
        if (found is not { } end) return false;

        var cells = new List<(int, int)> { end };
        var cursor = end;
        while (previous.TryGetValue(cursor, out var prev)) { cells.Add(prev); cursor = prev; }
        cells.Reverse();

        path = cells.Select(c => grid.CellCenter(c.Item1, c.Item2)).ToList();
        for (var i = 1; i < path.Count; i++)
        {
            length += Distance(path[i - 1], path[i]);
            if (firstJump is null && jumpedFrom.TryGetValue(cells[i], out var jumpPoint)) firstJump = (i - 1, jumpPoint);
        }
        return true;
    }

    // Every cell a jump starts from, with where it lands, what the leap costs and the jump point used; both ways. A jump whose
    // either end has no floor within JumpReachUnits on this grid is left out.
    private static Dictionary<(int, int), List<((int, int) Landing, float Cost, (float X, float Y) At)>> Takeoffs(
        WalkGrid grid, IReadOnlyList<JumpLink> jumps)
    {
        var takeoffs = new Dictionary<(int, int), List<((int, int), float, (float X, float Y))>>();
        foreach (var jump in jumps)
            foreach (var (from, to) in new[] { (jump.A, jump.B), (jump.B, jump.A) })
            {
                if (NearestFloor(grid, to) is not { } landing) continue;
                float leap = Distance(from, to) + JumpExtraCost;
                foreach (var cell in FloorNear(grid, from))
                {
                    if (!takeoffs.TryGetValue(cell, out var list)) takeoffs[cell] = list = [];
                    list.Add((landing, leap, from));
                }
            }
        return takeoffs;
    }

    private static IEnumerable<(int Row, int Col)> FloorNear(WalkGrid grid, (float X, float Y) point)
    {
        var (row, col) = grid.CellAt(point.X, point.Y);
        var span = (int)MathF.Ceiling(JumpReachUnits / grid.Resolution);
        for (var r = row - span; r <= row + span; r++)
            for (var c = col - span; c <= col + span; c++)
                if (grid.IsWalkable(r, c) && Distance(grid.CellCenter(r, c), point) <= JumpReachUnits) yield return (r, c);
    }

    private static (int Row, int Col)? NearestFloor(WalkGrid grid, (float X, float Y) point) =>
        FloorNear(grid, point).Cast<(int Row, int Col)?>().MinBy(c => Distance(grid.CellCenter(c!.Value.Row, c.Value.Col), point));

    /// <summary>The farthest path point still in a straight line of sight from the path's start —
    /// the route's real next heading, instead of a fixed look-ahead distance that lands on whichever
    /// grid-aligned direction the path happens to start in (live log 2026-09-28: BFS cell paths start
    /// along a grid axis, so a short fixed look-ahead read as a screen diagonal almost every time).
    /// Scans from the end back toward index 1 and returns the first point with clear sight of
    /// <c>path[0]</c>; falls back to <c>path[1]</c> if nothing beyond it is visible, or <c>path[0]</c>
    /// itself for a single-point path. The path between must stay within <see cref="BearingSlackUnits"/> of the line.</summary>
    public static (float X, float Y) FarthestVisiblePoint(WalkGrid grid, List<(float X, float Y)> path)
    {
        if (path.Count <= 1) return path.Count == 1 ? path[0] : (0f, 0f);

        for (var i = path.Count - 1; i >= 1; i--)
            if (FollowsPath(path, i) && HasLineOfSight(grid, path[0], path[i])) return path[i];

        return path[1];
    }

    // Every path point up to path[to] lies within BearingSlackUnits of the segment path[0]..path[to].
    private static bool FollowsPath(List<(float X, float Y)> path, int to)
    {
        for (var j = 1; j < to; j++)
            if (DistanceToSegment(path[j], path[0], path[to]) > BearingSlackUnits) return false;
        return true;
    }

    private static float DistanceToSegment((float X, float Y) p, (float X, float Y) a, (float X, float Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0f ? 0f : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0f, 1f);
        return Distance(p, (a.X + t * dx, a.Y + t * dy));
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
