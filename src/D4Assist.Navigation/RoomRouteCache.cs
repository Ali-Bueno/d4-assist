namespace D4Assist.Navigation;

/// <summary>One opening's resolved route: the direction to speak, the path length (exposed, not spoken
/// yet — task spec), whether BFS found a route or fell back to a straight line, and whether this is
/// the opening the player just entered through (excluded from the spoken list — see AnnouncementPolicy).</summary>
public sealed record OpeningRoute(RoomOpening Opening, StickDirection Direction, float PathLength, bool UsedPathFallback, bool IsEntry = false);

/// <summary>A traced route to an opening: the look-ahead point to head for, the path length (0 on fallback),
/// and whether BFS failed and the point is just the opening itself.</summary>
public sealed record PathBearing((float X, float Y) Point, float Length, bool UsedFallback);

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
    private List<OpeningRoute>? _cached;

    public IReadOnlyList<OpeningRoute> Resolve(Room room, PlayerInput player)
    {
        var playerPos = (player.X, player.Y);
        if (_cached is not null && room.Key == _cachedKey && Distance(playerPos, _cachedPlayer) <= RecomputeThresholdUnits)
            return _cached;

        var routes = room.Openings.Select(opening => Resolve(room, playerPos, opening)).ToList();

        _cachedKey = room.Key;
        _cachedPlayer = playerPos;
        _cached = routes;
        return routes;
    }

    /// <summary>BFS path from the player to <paramref name="goal"/> inside the room, and the farthest visible
    /// point along it. Falls back to the opening's own position when the room has no grid or no path.</summary>
    public static PathBearing Trace(Room room, (float X, float Y) player, RoomOpening goal)
    {
        if (room.NavGrid is { } grid &&
            RoomPathfinder.TryFindPath(grid, player, InwardTargets(grid, goal), out var path, out var length))
        {
            var farthest = RoomPathfinder.FarthestVisiblePoint(grid, path);
            return new PathBearing(Distance(player, farthest) < MinBearingDistanceUnits ? goal.Position : farthest, length, false);
        }
        return new PathBearing(goal.Position, 0f, true);
    }

    private static OpeningRoute Resolve(Room room, (float X, float Y) player, RoomOpening opening)
    {
        var trace = Trace(room, player, opening);
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
