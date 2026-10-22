using System.Numerics;

namespace D4Assist.Navigation;

/// <summary>
/// Shortest walk over the open world's surface voxels (scene m_cellBytes surfaces with their heights,
/// research/dungeon-data.md §4.1b), for a quest place the game's own navigation doesn't lead to. The surfaces form a
/// 2.5D map: a column may hold floors at several heights, and a step joins two of them only when their heights differ by
/// at most <see cref="MaxStepUnits"/>, so a cliff's top and foot, side by side seen from above, stay apart.
/// </summary>
/// <remarks>A step near an edge costs more, as in RoomPathfinder, so the route keeps to the middle of a path or a ridge.
/// Searched with A*: a 400-unit stretch of the Dry Steppes (24 chunks, ~290,000 surface columns) takes well under a second.</remarks>
public static class SurfacePathfinder
{
    /// <summary>Edge of a surface voxel in world units.</summary>
    public const float VoxelUnits = 0.5f;

    /// <summary>The largest height change between neighbouring surface voxels a walk takes (a 63° slope at 0.5-unit steps):
    /// walkable slopes only ever hold surface voxels, so a larger change is a drop or a wall between two floors.</summary>
    public const float MaxStepUnits = 1f;

    /// <summary>Heights in one column closer than this are one floor.</summary>
    private const float SameFloorUnits = 0.75f;

    /// <summary>How far to look for floor under the start or the target.</summary>
    public const float SnapUnits = 4f;

    /// <summary>The target counts as reached on any floor this close to it (a quest place is a trigger sphere, not a spot).</summary>
    public const float TargetReachUnits = 2f;

    /// <summary>Spacing of the points the route returns.</summary>
    public const float PointSpacingUnits = 5f;

    private static readonly (int Dx, int Dy, float Length)[] Moves =
    [
        (-1, 0, 1f), (1, 0, 1f), (0, -1, 1f), (0, 1, 1f),
        (-1, -1, MathF.Sqrt(2)), (-1, 1, MathF.Sqrt(2)), (1, -1, MathF.Sqrt(2)), (1, 1, MathF.Sqrt(2)),
    ];

    // The extra cost, per unit walked, of a step onto a voxel this many voxels from an edge (none from 4 on: 2 units).
    private static float EdgePenalty(int clearance) => clearance switch { 0 or 1 => 4f, 2 or 3 => 1f, _ => 0f };

    /// <summary>The route from <paramref name="from"/> to <paramref name="to"/> as points about
    /// <see cref="PointSpacingUnits"/> apart (the last one the target), or null when no walk joins them.</summary>
    /// <param name="surfaces">Surface voxels in world space (each voxel's low corner, Z its floor).</param>
    /// <param name="fromZ">The player's height, to pick the floor they stand on; null for the lowest.</param>
    public static IReadOnlyList<Vector2>? Find(IEnumerable<Vector3> surfaces, Vector2 from, Vector2 to, float? fromZ = null)
    {
        var map = SurfaceMap.Build(surfaces);
        if (map is null || map.Snap(from, fromZ) is not { } start) return null;
        var goals = map.NodesNear(to, TargetReachUnits);
        if (goals.Count == 0 && map.Snap(to, null) is { } nearTarget) goals.Add(nearTarget);
        if (goals.Count == 0) return null;
        var path = map.Search(start, goals, to);
        return path is null ? null : Sample(path, to);
    }

    // Points along the path every PointSpacingUnits, ending at the target itself.
    private static List<Vector2> Sample(List<Vector2> path, Vector2 target)
    {
        var points = new List<Vector2>();
        float walked = 0;
        for (int i = 1; i < path.Count; i++)
        {
            walked += Vector2.Distance(path[i - 1], path[i]);
            if (walked < PointSpacingUnits) continue;
            points.Add(path[i]);
            walked = 0;
        }
        if (points.Count == 0 || Vector2.Distance(points[^1], target) > 0.01f) points.Add(target);
        return points;
    }

    /// <summary>The surface voxels as columns of floors: node = (column, floor), floors listed per column.</summary>
    private sealed class SurfaceMap
    {
        private readonly int _x0, _y0, _width, _height;
        private readonly int[] _first; // per column: index of its first floor in _floors; columns end where the next starts
        private readonly float[] _floors;

        private SurfaceMap(int x0, int y0, int width, int height, int[] first, float[] floors) =>
            (_x0, _y0, _width, _height, _first, _floors) = (x0, y0, width, height, first, floors);

        public static SurfaceMap? Build(IEnumerable<Vector3> surfaces)
        {
            var byColumn = new Dictionary<(int X, int Y), List<float>>();
            foreach (var v in surfaces)
            {
                var key = ((int)MathF.Floor(v.X / VoxelUnits), (int)MathF.Floor(v.Y / VoxelUnits));
                if (!byColumn.TryGetValue(key, out var heights)) byColumn[key] = heights = [];
                int same = heights.FindIndex(h => MathF.Abs(h - v.Z) < SameFloorUnits);
                if (same < 0) heights.Add(v.Z);
                else heights[same] = MathF.Max(heights[same], v.Z);
            }
            if (byColumn.Count == 0) return null;

            int x0 = byColumn.Keys.Min(k => k.X), y0 = byColumn.Keys.Min(k => k.Y);
            int width = byColumn.Keys.Max(k => k.X) - x0 + 1, height = byColumn.Keys.Max(k => k.Y) - y0 + 1;
            var first = new int[width * height + 1];
            var floors = new List<float>();
            for (int c = 0; c < width * height; c++)
            {
                first[c] = floors.Count;
                if (byColumn.TryGetValue((c / height + x0, c % height + y0), out var heights)) floors.AddRange(heights);
            }
            first[^1] = floors.Count;
            return new SurfaceMap(x0, y0, width, height, first, [.. floors]);
        }

        private int Column(int x, int y) => x * _height + y;

        private float Height(int node) => _floors[node];

        // Floor nodes of the column at grid (x, y); empty outside the map.
        private (int From, int To) Floors(int x, int y) =>
            x < 0 || y < 0 || x >= _width || y >= _height ? (0, 0) : (_first[Column(x, y)], _first[Column(x, y) + 1]);

        private (int X, int Y) Cell(Vector2 p) => ((int)MathF.Floor(p.X / VoxelUnits) - _x0, (int)MathF.Floor(p.Y / VoxelUnits) - _y0);

        private Vector2 Centre(int x, int y) => new((x + _x0 + 0.5f) * VoxelUnits, (y + _y0 + 0.5f) * VoxelUnits);

        private (int X, int Y) CellOf(int node)
        {
            int column = Array.BinarySearch(_first, node);
            if (column < 0) column = ~column - 1;
            while (_first[column + 1] == _first[column]) column++; // empty columns share their start with the next one
            return (column / _height, column % _height);
        }

        /// <summary>The floor nearest the point (the one closest to <paramref name="z"/>, or the lowest), within SnapUnits.</summary>
        public int? Snap(Vector2 p, float? z)
        {
            var (cx, cy) = Cell(p);
            int radius = (int)MathF.Ceiling(SnapUnits / VoxelUnits);
            for (int r = 0; r <= radius; r++)
            {
                int? best = null;
                float bestScore = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var (from, to) = Floors(cx + dx, cy + dy);
                        for (int n = from; n < to; n++)
                        {
                            float score = z is { } h ? MathF.Abs(Height(n) - h) : Height(n);
                            if (score < bestScore) (best, bestScore) = (n, score);
                        }
                    }
                if (best is not null) return best;
            }
            return null;
        }

        public HashSet<int> NodesNear(Vector2 p, float radius)
        {
            var (cx, cy) = Cell(p);
            int cells = (int)MathF.Ceiling(radius / VoxelUnits);
            var nodes = new HashSet<int>();
            for (int dx = -cells; dx <= cells; dx++)
                for (int dy = -cells; dy <= cells; dy++)
                {
                    if (Vector2.Distance(Centre(cx + dx, cy + dy), p) > radius) continue;
                    var (from, to) = Floors(cx + dx, cy + dy);
                    for (int n = from; n < to; n++) nodes.Add(n);
                }
            return nodes;
        }

        // The floors a walk steps onto from node n at (x, y), with the move's length.
        private IEnumerable<(int Node, int X, int Y, float Length)> Steps(int n, int x, int y)
        {
            float z = Height(n);
            foreach (var (dx, dy, length) in Moves)
            {
                if (dx != 0 && dy != 0 && !(HasFloorNear(x + dx, y, z) && HasFloorNear(x, y + dy, z))) continue; // no cut corners
                var (from, to) = Floors(x + dx, y + dy);
                for (int m = from; m < to; m++)
                    if (MathF.Abs(Height(m) - z) <= MaxStepUnits) yield return (m, x + dx, y + dy, length);
            }
        }

        private bool HasFloorNear(int x, int y, float z)
        {
            var (from, to) = Floors(x, y);
            for (int n = from; n < to; n++)
                if (MathF.Abs(Height(n) - z) <= MaxStepUnits) return true;
            return false;
        }

        // Voxels from the nearest edge (a floor with a neighbour it can't step to), up to 4, over the nodes the search reaches.
        private int Clearance(int n, int x, int y, Dictionary<int, int> cache)
        {
            if (cache.TryGetValue(n, out int known)) return known;
            const int Max = 4;
            int clearance = Max;
            var (fx, fy) = (x, y);
            float z = Height(n);
            for (int r = 0; r < Max && clearance == Max; r++)
                for (int dx = -r; dx <= r && clearance == Max; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        if (!IsInner(fx + dx, fy + dy, z, r)) { clearance = r; break; }
                    }
            cache[n] = clearance;
            return clearance;
        }

        // True when the column at (x, y) has a floor near z (within a step per ring walked) whose eight neighbours all have one too.
        private bool IsInner(int x, int y, float z, int ring)
        {
            float reach = MaxStepUnits * (ring + 1);
            var (from, to) = Floors(x, y);
            for (int n = from; n < to; n++)
            {
                if (MathF.Abs(Height(n) - z) > reach) continue;
                int neighbours = 0;
                foreach (var (dx, dy, _) in Moves)
                {
                    var (nf, nt) = Floors(x + dx, y + dy);
                    for (int m = nf; m < nt; m++)
                        if (MathF.Abs(Height(m) - Height(n)) <= MaxStepUnits) { neighbours++; break; }
                }
                if (neighbours == Moves.Length) return true;
            }
            return false;
        }

        public List<Vector2>? Search(int start, HashSet<int> goals, Vector2 target)
        {
            var cost = new Dictionary<int, float> { [start] = 0 };
            var previous = new Dictionary<int, int>();
            var clearance = new Dictionary<int, int>();
            var open = new PriorityQueue<(int Node, int X, int Y), float>();
            var (sx, sy) = CellOf(start);
            open.Enqueue((start, sx, sy), Remaining(sx, sy, target));
            var closed = new HashSet<int>();
            while (open.TryDequeue(out var current, out _))
            {
                if (!closed.Add(current.Node)) continue;
                if (goals.Contains(current.Node)) return Trace(current.Node, previous);
                float here = cost[current.Node];
                foreach (var (next, x, y, length) in Steps(current.Node, current.X, current.Y))
                {
                    if (closed.Contains(next)) continue;
                    float step = length * VoxelUnits * (1 + EdgePenalty(Clearance(next, x, y, clearance)));
                    if (cost.TryGetValue(next, out float known) && known <= here + step) continue;
                    cost[next] = here + step;
                    previous[next] = current.Node;
                    open.Enqueue((next, x, y), here + step + Remaining(x, y, target));
                }
            }
            return null;
        }

        private float Remaining(int x, int y, Vector2 target) => Vector2.Distance(Centre(x, y), target);

        private List<Vector2> Trace(int node, Dictionary<int, int> previous)
        {
            var path = new List<Vector2>();
            for (int n = node; ; n = previous[n])
            {
                var (x, y) = CellOf(n);
                path.Add(Centre(x, y));
                if (!previous.ContainsKey(n)) break;
            }
            path.Reverse();
            return path;
        }
    }
}
