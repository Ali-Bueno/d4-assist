namespace D4Assist.Navigation;

/// <summary>One opening's resolved route: the direction to speak, the path length (exposed, not spoken
/// yet — task spec), whether BFS found a route or fell back to a straight line, and whether this is
/// the opening the player just entered through (excluded from the spoken list — see AnnouncementPolicy).</summary>
public sealed record OpeningRoute(RoomOpening Opening, StickDirection Direction, float PathLength, bool UsedPathFallback, bool IsEntry = false);

/// <summary>A traced route to an opening: the look-ahead point to head for, the path length (0 on fallback),
/// and whether BFS failed and the point is just the opening itself. <paramref name="JumpAt"/>: the jump point the route
/// must use first, which the point then leads to; null when it needs none.</summary>
public sealed record PathBearing((float X, float Y) Point, float Length, bool UsedFallback, (float X, float Y)? JumpAt = null);

/// <summary>
/// Resolves each opening's spoken direction via a BFS path's farthest visible point (RoomPathfinder)
/// instead of a straight line (crosses walls on an irregular room) or a fixed short look-ahead (reads
/// as whichever grid axis the path starts along — the room-announcement bug). Caches per room and
/// only recomputes when the player moves more than <see cref="RecomputeThresholdUnits"/>.
/// </summary>
public sealed class RoomRouteCache
{
    /// <summary>Below this distance from the player, the farthest-visible-point bearing is noise (the
    /// point barely moved from the player's own cell); use the opening's own position instead.</summary>
    public const float MinBearingDistanceUnits = 1f;

    /// <summary>Movement, in world units, that invalidates the cached routes: bigger than one grid cell
    /// so routes aren't rebuilt every poll, smaller than a DRLG tile (32) so a real move is still caught
    /// (task spec: "recompute when the player moves more than a named threshold").</summary>
    public const float RecomputeThresholdUnits = 2f;

    // Steps inward from the opening's boundary point (which sits exactly on the tile edge, not
    // necessarily a walkable cell centre) used as BFS targets — a few cells is enough to land inside.
    private const int InwardTargetSteps = 3;

    private RoomKey _cachedKey = RoomKey.Invalid;
    private (float X, float Y) _cachedPlayer;
    private IReadOnlyList<JumpLink>? _cachedJumps;
    private List<OpeningRoute>? _cached;

    /// <param name="jumps">The loaded tiles' jump links (ExplorationGuide.JumpLinks), which a route may cross.</param>
    public IReadOnlyList<OpeningRoute> Resolve(Room room, PlayerInput player, IReadOnlyList<JumpLink>? jumps = null)
    {
        var playerPos = (player.X, player.Y);
        if (_cached is not null && room.Key == _cachedKey && ReferenceEquals(jumps, _cachedJumps)
            && Distance(playerPos, _cachedPlayer) <= RecomputeThresholdUnits)
            return _cached;

        var routes = room.Openings.Select(opening => Resolve(room, playerPos, opening, jumps)).ToList();

        _cachedKey = room.Key;
        _cachedPlayer = playerPos;
        _cachedJumps = jumps;
        _cached = routes;
        return routes;
    }

    /// <summary>Whether a walk from the player reaches the opening; an opening with no floor run (a midpoint guess) always counts.</summary>
    public static bool Reached(OpeningRoute route) => route.Opening.UsedFallback || !route.UsedPathFallback;

    /// <summary>BFS path from the player to <paramref name="goal"/> inside the room, and the farthest visible
    /// point along it, around <paramref name="props"/> where it can. Falls back to the opening's own position when the room has no grid or no path.</summary>
    public static PathBearing Trace(
        Room room, (float X, float Y) player, RoomOpening goal, IReadOnlyList<JumpLink>? jumps = null, BlockedCells? props = null)
    {
        if (room.NavGrid is not { } grid) return new PathBearing(goal.Position, 0f, true);
        if (Doorway(grid, player, goal) is { } beyond) return new PathBearing(beyond, Distance(player, goal.Position), false);
        if (RoomPathfinder.TryFindPath(grid, player, InwardTargets(grid, goal), jumps ?? [], out var path, out var length, out var jump, props))
            return Bearing(grid, player, path, length, jump, goal.Position, props);
        return new PathBearing(goal.Position, 0f, true);
    }

    // In the opening's doorway, no deeper than its inward targets, the way on is through it, to a point past its edge. The
    // nearest target lay behind a player standing there: a step short of the mindscape entrance tile's way into the next one,
    // the guide pointed back towards the exit portal, and the player turned back twice (live 2026-10-21).
    private static (float X, float Y)? Doorway(WalkGrid grid, (float X, float Y) player, RoomOpening goal)
    {
        var (nx, ny) = ExitMaskBits.Normal(goal.Side);
        var (ax, ay) = (MathF.Abs(ny), MathF.Abs(nx));
        float band = InwardTargetSteps * grid.Resolution;
        float depth = (goal.Position.X - player.X) * nx + (goal.Position.Y - player.Y) * ny;
        float along = (player.X - goal.Position.X) * ax + (player.Y - goal.Position.Y) * ay;
        float half = MathF.Max(goal.Width / 2f, grid.Resolution);
        if (depth < -grid.Resolution || depth > band || MathF.Abs(along) > half) return null;
        float onto = Math.Clamp(along, -MathF.Max(half - grid.Resolution, 0f), MathF.Max(half - grid.Resolution, 0f));
        return (goal.Position.X + ax * onto + nx * band, goal.Position.Y + ay * onto + ny * band);
    }

    /// <summary>BFS path from the player to the floor within <paramref name="reach"/> of <paramref name="point"/> (an object stands
    /// on cells of its own), and the farthest visible point along it, around <paramref name="props"/> where it can. Falls back to the
    /// point itself when the room has no grid or no path.</summary>
    public static PathBearing TraceToPoint(
        Room room, (float X, float Y) player, (float X, float Y) point, float reach, IReadOnlyList<JumpLink>? jumps = null, BlockedCells? props = null)
    {
        if (room.NavGrid is { } grid &&
            RoomPathfinder.TryFindPath(grid, player, FloorWithin(grid, point, reach), jumps ?? [], out var path, out var length, out var jump, props))
            return Bearing(grid, player, path, length, jump, point, props);
        return new PathBearing(point, 0f, true);
    }

    // Where to head on a found path: its farthest visible point, or, when the path needs a jump, the farthest visible point
    // before it and, once near, the jump point itself: the line across the gap is no way to walk.
    private static PathBearing Bearing(WalkGrid grid, (float X, float Y) player, List<(float X, float Y)> path, float length,
        (int Index, (float X, float Y) At)? jump, (float X, float Y) goal, BlockedCells? props)
    {
        if (jump is { } leap)
        {
            var toward = RoomPathfinder.FarthestVisiblePoint(grid, path.GetRange(0, leap.Index + 1), props);
            bool near = Distance(player, leap.At) <= RoomPathfinder.JumpReachUnits || Distance(player, toward) < MinBearingDistanceUnits;
            return new PathBearing(near ? leap.At : toward, length, false, leap.At);
        }
        var farthest = RoomPathfinder.FarthestVisiblePoint(grid, path, props);
        return new PathBearing(Distance(player, farthest) < MinBearingDistanceUnits ? goal : farthest, length, false);
    }

    private static List<(int Row, int Col)> FloorWithin(WalkGrid grid, (float X, float Y) point, float reach)
    {
        var (row, col) = grid.CellAt(point.X, point.Y);
        var span = (int)MathF.Ceiling(reach / grid.Resolution);
        var cells = new List<(int, int)>();
        for (var r = row - span; r <= row + span; r++)
            for (var c = col - span; c <= col + span; c++)
                if (grid.IsWalkable(r, c) && Distance(grid.CellCenter(r, c), point) <= reach) cells.Add((r, c));
        return cells;
    }

    private static OpeningRoute Resolve(Room room, (float X, float Y) player, RoomOpening opening, IReadOnlyList<JumpLink>? jumps)
    {
        var trace = Trace(room, player, opening, jumps);
        var direction = ScreenDirections.Bin(trace.Point.X - player.X, trace.Point.Y - player.Y);
        return new OpeningRoute(opening, direction, trace.Length, trace.UsedFallback);
    }

    private static List<(int Row, int Col)> InwardTargets(WalkGrid grid, RoomOpening opening)
    {
        var (nx, ny) = ExitMaskBits.Normal(opening.Side);
        var targets = new List<(int, int)>();
        for (var step = 1; step <= InwardTargetSteps; step++)
            targets.Add(grid.CellAt(opening.Position.X - nx * step * grid.Resolution, opening.Position.Y - ny * step * grid.Resolution));
        return targets;
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) =>
        MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2));
}
