namespace D4Assist.Navigation;

/// <summary>
/// Leads the player to the nearest unexplored part of the dungeon: a known tile not yet explored (stood in, or seen from a connected doorway), or an exit
/// whose far side never loaded (a frontier). "Nearest" is route length over the tile graph; the next waypoint
/// is the look-ahead point on the room's walk-grid path to the opening that leaves the player's room.
/// </summary>
public sealed class ExplorationGuide
{
    /// <summary>A challenger must beat the current target's route by this much to replace it: one DRLG tile
    /// (half a tile still flip-flopped in the 2026-09-28 Undercity log, routes crossing by 10-20 units).</summary>
    public const float SwitchMarginUnits = GridMath.CellSize;

    /// <summary>Never switch away from a target whose route is shorter than this (one tile) while it is still a candidate.</summary>
    public const float CommitDistanceUnits = GridMath.CellSize;

    /// <summary>Standing this close to the centre of a directly connected tile counts as seeing it: a 32-unit tile's
    /// centre is 16 units from its doorway, and the Undercity exit pad became visible at 22-34 units live
    /// (research/dungeon-objectives.md section 5), so a doorway peek is enough to learn a dead end holds nothing.</summary>
    public const float SeenRadiusUnits = 20f;

    /// <summary>Standing this close to a frontier whose far side still hasn't loaded means the exit leads nowhere:
    /// a quarter tile, well inside the loaded window (~2 tiles around the player).</summary>
    public const float FrontierArrivalUnits = GridMath.CellSize / 4f;

    private sealed record Candidate(GuideTarget Target, float Cost, TileNode Node, TileHop? FrontierHop);

    private readonly DungeonMap _map;
    private readonly ITileWalkability? _walkability;
    private readonly Func<int, bool> _isExitCandidate;
    private readonly HashSet<string> _spentFrontiers = [];
    // Re-check mode: exit-candidate tiles passed this round; _held ones were still under the player when a round restarted.
    private readonly HashSet<string> _rechecked = [];
    private readonly HashSet<string> _held = [];
    private int _recheckVersion;
    private int _round = 1;
    private bool _rechecking;
    private bool _cachedExitMissing;
    private int _cachedRecheck = -1;

    private TileGraph? _graph;
    private int _graphVersion = -1;
    private Candidate? _current;
    private GuideStep? _cached;
    private (float X, float Y) _cachedPlayer;
    private int _cachedVersion = -1;
    private int _cachedSpent = -1;

    /// <param name="isExitCandidate">True for a scene SNO that can host the floor exit (game data); default none.</param>
    public ExplorationGuide(DungeonMap map, ITileWalkability? walkability = null, Func<int, bool>? isExitCandidate = null)
    {
        _map = map;
        _walkability = walkability;
        _isExitCandidate = isExitCandidate ?? (_ => false);
    }

    /// <summary>Forget the current target and every dead exit. Call on dungeon change.</summary>
    public void Reset()
    {
        _spentFrontiers.Clear();
        _rechecked.Clear();
        _held.Clear();
        _recheckVersion = 0;
        _round = 1;
        _rechecking = false;
        _graph = null;
        _graphVersion = -1;
        _current = null;
        _cached = null;
        Arrivals = 0;
    }

    /// <summary>How many targets the player has reached since the last <see cref="Reset"/> (explored, or a dead exit stood at).</summary>
    public int Arrivals { get; private set; }

    /// <param name="exitMissing">True while the caller still lacks the floor exit: with nothing left to explore the guide re-checks the exit-candidate tiles.</param>
    public GuideStep Step(PlayerInput player, bool exitMissing = false)
    {
        if (_map.TileAt(player.X, player.Y) is not { } tile) return GuideStep.Unknown;

        var graph = CurrentGraph();
        MarkSeenNeighbours(graph, tile, player);
        SpendReachedFrontiers(graph, player);
        if (exitMissing && _rechecking) MarkRechecked(graph, player);

        var here = (player.X, player.Y);
        if (_cached is not null && _cachedVersion == _map.Version && _cachedSpent == _spentFrontiers.Count &&
            _cachedRecheck == _recheckVersion && _cachedExitMissing == exitMissing &&
            TileRoutes.Distance(here, _cachedPlayer) <= RoomRouteCache.RecomputeThresholdUnits)
            return _cached;

        _cached = Compute(graph, tile, player, exitMissing);
        _cachedPlayer = here;
        _cachedVersion = _map.Version;
        _cachedSpent = _spentFrontiers.Count;
        _cachedRecheck = _recheckVersion;
        _cachedExitMissing = exitMissing;
        return _cached;
    }

    private TileGraph CurrentGraph()
    {
        if (_graph is null || _graphVersion != _map.Version)
        {
            _graph = TileGraph.Build(_map, _walkability);
            _graphVersion = _map.Version;
        }
        return _graph;
    }

    private GuideStep Compute(TileGraph graph, MapTile tile, PlayerInput player, bool exitMissing)
    {
        var start = graph.NodeAt(tile)!;
        var here = (player.X, player.Y);
        var routes = TileRoutes.From(start, here);

        var candidates = Candidates(routes, start).ToList();
        if (_current is { } previous && candidates.All(c => c.Target.Id != previous.Target.Id) && Reached(previous)) Arrivals++;
        _rechecking = false;
        if (candidates.Count == 0 && exitMissing)
        {
            candidates = RecheckCandidates(routes, player);
            _rechecking = candidates.Count > 0;
        }
        if (candidates.Count == 0)
        {
            _current = null;
            return GuideStep.AllExplored with { Arrivals = Arrivals };
        }

        var best = candidates.MinBy(c => c.Cost)!;
        var current = candidates.FirstOrDefault(c => c.Target.Id == _current?.Target.Id);
        var keep = current is not null && (current.Cost < CommitDistanceUnits || best.Cost > current.Cost - SwitchMarginUnits);
        var chosen = keep ? current! : best;
        _current = chosen;

        var hops = routes.HopsTo(chosen.Node);
        if (chosen.FrontierHop is { } exit) hops.Add(new RouteHop(exit.Opening, null, chosen.Cost));
        float remaining = chosen.Cost;
        var status = _rechecking ? GuideStatus.Rechecking : GuideStatus.Guiding;
        var waypoint = hops.Count == 0 ? chosen.Target.Position : Waypoint(hops, chosen.Cost, player, out remaining); // no hops: the target is in the player's tile
        return new GuideStep(status, chosen.Target, waypoint, remaining, Arrivals, _rechecking ? _round : 0);
    }

    // A target left the candidate list because the player got there, not because its far side merely loaded.
    private bool Reached(Candidate target) =>
        target.Target.Kind == GuideTargetKind.ExitCandidate ? false // counted when passed, see MarkRechecked
        : target.FrontierHop is null ? _map.IsVisited(target.Node.Tile) : _spentFrontiers.Contains(target.Target.Id);

    private IEnumerable<Candidate> Candidates(TileRoutes routes, TileNode start)
    {
        foreach (var node in routes.Reached)
        {
            if (!_map.IsVisited(node.Tile))
            {
                if (node != start)
                    yield return new Candidate(
                        new GuideTarget(GuideTargetKind.UnvisitedTile, $"tile:{node.Tile.CellX},{node.Tile.CellY}", routes.EntryPoint(node)),
                        routes.LengthTo(node), node, null);
                continue;
            }
            foreach (var hop in node.Hops.Where(h => h.To is null && !_spentFrontiers.Contains(FrontierId(node, h.Opening))))
                yield return new Candidate(
                    new GuideTarget(GuideTargetKind.Frontier, FrontierId(node, hop.Opening), hop.Opening.Position),
                    routes.LengthTo(node) + TileRoutes.Distance(routes.EntryPoint(node), hop.Opening.Position), node, hop);
        }
    }

    // Reachable exit-candidate tiles not yet passed this round; when the round just ended a new one starts over every candidate.
    private List<Candidate> RecheckCandidates(TileRoutes routes, PlayerInput player)
    {
        var all = routes.Reached.Where(n => _isExitCandidate(n.Tile.Input.SceneSno)).ToList();
        var pending = all.Where(n => !_rechecked.Contains(RecheckId(n.Tile))).ToList();
        if (pending.Count == 0 && all.Count > 0)
        {
            _round++;
            _rechecked.Clear();
            _held.Clear();
            foreach (var n in all.Where(n => Near(n.Tile, player)))
            {
                _rechecked.Add(RecheckId(n.Tile));
                _held.Add(RecheckId(n.Tile));
            }
            _recheckVersion++;
            pending = all.Where(n => !_rechecked.Contains(RecheckId(n.Tile))).ToList();
            if (pending.Count == 0) pending = all; // only tiles under the player: keep leading to them
        }
        return pending.Select(n => new Candidate(
            new GuideTarget(GuideTargetKind.ExitCandidate, RecheckId(n.Tile), routes.EntryPoint(n)), routes.LengthTo(n), n, null)).ToList();
    }

    // Passing within SeenRadiusUnits of a candidate's centre re-checks it; a held one re-arms once the player walks away.
    private void MarkRechecked(TileGraph graph, PlayerInput player)
    {
        foreach (var node in graph.Nodes.Where(n => _isExitCandidate(n.Tile.Input.SceneSno)))
        {
            var id = RecheckId(node.Tile);
            if (!Near(node.Tile, player))
            {
                if (_held.Remove(id)) { _rechecked.Remove(id); _recheckVersion++; }
            }
            else if (_rechecked.Add(id))
            {
                _recheckVersion++;
                if (_current?.Target.Id == id) Arrivals++;
            }
        }
    }

    private static bool Near(MapTile tile, PlayerInput player)
    {
        var input = tile.Input;
        return TileRoutes.Distance((player.X, player.Y), (input.OriginX + input.SizeX / 2f, input.OriginY + input.SizeY / 2f)) <= SeenRadiusUnits;
    }

    private static string RecheckId(MapTile tile) => $"exit-candidate:{tile.CellX},{tile.CellY}";

    // Heads for the first opening that leaves the player's room; a target inside the room is the route's last hop.
    private (float X, float Y) Waypoint(List<RouteHop> hops, float total, PlayerInput player, out float remaining)
    {
        var room = RoomModel.TryGetRoom(_map, player.X, player.Y, out var found, _walkability) ? found : null;
        var roomTiles = room?.Tiles.Select(TileGraph.Key).ToHashSet() ?? [];
        var hop = hops.FirstOrDefault(h => h.To is null || !roomTiles.Contains(TileGraph.Key(h.To.Tile))) ?? hops[^1];
        if (room is null)
        {
            remaining = total;
            return hop.Opening.Position;
        }

        var trace = RoomRouteCache.Trace(room, (player.X, player.Y), hop.Opening);
        remaining = trace.UsedFallback ? total : trace.Length + (total - hop.LengthSoFar);
        return trace.Point;
    }

    // A sighted player reads a dead end from its doorway: a tile joined to the player's by a real opening (never across
    // a wall) is explored once the player is within SeenRadiusUnits of its centre. Big tiles still need entering.
    private void MarkSeenNeighbours(TileGraph graph, MapTile tile, PlayerInput player)
    {
        if (graph.NodeAt(tile) is not { } node) return;
        foreach (var hop in node.Hops)
        {
            if (hop.To is not { } next || _map.IsVisited(next.Tile)) continue;
            var input = next.Tile.Input;
            var centre = (input.OriginX + input.SizeX / 2f, input.OriginY + input.SizeY / 2f);
            if (TileRoutes.Distance((player.X, player.Y), centre) <= SeenRadiusUnits) _map.MarkSeen(next.Tile);
        }
    }

    private void SpendReachedFrontiers(TileGraph graph, PlayerInput player)
    {
        foreach (var node in graph.Nodes)
            foreach (var hop in node.Hops)
                if (hop.To is null && TileRoutes.Distance((player.X, player.Y), hop.Opening.Position) <= FrontierArrivalUnits)
                    _spentFrontiers.Add(FrontierId(node, hop.Opening));
    }

    private static string FrontierId(TileNode node, RoomOpening opening) =>
        $"exit:{node.Tile.CellX},{node.Tile.CellY}:{opening.Side}:{MathF.Round(opening.Position.X)},{MathF.Round(opening.Position.Y)}";
}
