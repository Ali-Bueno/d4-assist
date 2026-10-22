namespace D4Assist.Navigation;

/// <summary>
/// Leads the player to an objective seen earlier, else to the nearest unexplored part of the dungeon: a known tile not yet explored (stood in, or seen from a connected doorway), or an exit
/// whose far side never loaded (a frontier); with nothing left, to the caller's known destinations (portals seen earlier).
/// "Nearest" is route length over the tile graph, plus the straight distance on to an objective known beyond the explored map; the next
/// waypoint is the look-ahead point on the room's walk-grid path to the opening that leaves the player's room, or to the objective itself
/// once it is in the player's room. Once a barrier falls (a door unlocked, a wall gone) the ground it held back is explored first.
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

    /// <summary>Standing this close to a destination reaches it: the same quarter tile, where its beacon loop takes over.</summary>
    public const float DestinationArrivalUnits = FrontierArrivalUnits;

    /// <param name="Reach">How close to a point target counts as there (GuideDestination.Radius); 0 for the arrival distance.</param>
    private sealed record Candidate(GuideTarget Target, float Cost, TileNode Node, TileHop? FrontierHop, float Reach = 0);

    private readonly DungeonMap _map;
    private readonly ITileWalkability? _walkability;
    private readonly Func<int, bool> _isExitCandidate;
    private readonly HashSet<string> _spentFrontiers = [];
    private readonly HashSet<string> _reachedDestinations = [];
    // Re-check mode: exit-candidate tiles passed this round; _held ones were still under the player when a round restarted.
    private readonly HashSet<string> _rechecked = [];
    private readonly HashSet<string> _held = [];
    private int _recheckVersion;
    private BlockedCells _blocked = BlockedCells.None;
    private int _blockedVersion;
    // The barriers as they stood before some fell, until the next step notes the ground they held back.
    private BlockedCells? _beforeOpening;
    // What the player could reach before the last barrier fell (tiles, and dead exits by id): it waits while ground past it is left.
    private HashSet<(int, int)> _behindTiles = [];
    private HashSet<string> _behindFrontiers = [];
    private int _round = 1;
    private bool _rechecking;
    private bool _cachedExitMissing;
    private int _cachedRecheck = -1;

    private TileGraph? _graph;
    private int _graphVersion = -1;
    private int _graphBlocked = -1;
    private Candidate? _current;
    private GuideStep? _cached;
    private (float X, float Y) _cachedPlayer;
    private int _cachedVersion = -1;
    private int _cachedBlocked = -1;
    private int _cachedSpent = -1;
    private string _cachedDestinations = "";

    /// <param name="isExitCandidate">True for a scene SNO that can host the floor exit (game data); default none.</param>
    public ExplorationGuide(DungeonMap map, ITileWalkability? walkability = null, Func<int, bool>? isExitCandidate = null)
    {
        _map = map;
        _walkability = walkability;
        _isExitCandidate = isExitCandidate ?? (_ => false);
    }

    /// <summary>Live barriers to route around (1-unit world cells, see <see cref="BlockedCells"/>); empty for none. Compared by content, so
    /// the caller may pass a fresh set every poll: only a different set rebuilds the routes.</summary>
    public void SetBlockedCells(IReadOnlySet<(int X, int Y)> cells)
    {
        if (_blocked.Cells.SetEquals(cells)) return;
        if (!_blocked.Cells.IsSubsetOf(cells)) _beforeOpening ??= _blocked;
        _blocked = new BlockedCells(cells);
        _blockedVersion++;
    }

    /// <summary>The jump links of the loaded tiles: the routes inside a room may use them, and the guide then leads to the
    /// jump point first. Compared by content, like <see cref="SetBlockedCells"/>.</summary>
    public void SetJumpLinks(IReadOnlyList<JumpLink> jumps)
    {
        if (jumps.SequenceEqual(_jumps)) return;
        _jumps = jumps;
        _jumpsVersion++;
    }

    private IReadOnlyList<JumpLink> _jumps = [];
    private int _jumpsVersion, _cachedJumps = -1;

    /// <summary>Forget the current target, every dead exit and the barriers. Call on dungeon change.</summary>
    public void Reset()
    {
        _spentFrontiers.Clear();
        _reachedDestinations.Clear();
        _rechecked.Clear();
        _held.Clear();
        _recheckVersion = 0;
        _round = 1;
        _rechecking = false;
        _blocked = BlockedCells.None;
        _jumps = [];
        _jumpsVersion++;
        _beforeOpening = null;
        _behindTiles = [];
        _behindFrontiers = [];
        _graph = null;
        _graphVersion = -1;
        _current = null;
        _cached = null;
        Arrivals = 0;
    }

    /// <summary>How many targets the player has reached since the last <see cref="Reset"/> (explored, or a dead exit stood at).</summary>
    public int Arrivals { get; private set; }

    /// <param name="exitMissing">True while the caller still lacks the floor exit: with nothing left to explore the guide re-checks the exit-candidate tiles.</param>
    /// <param name="destinations">Where to lead once nothing is left to explore (after any re-check); null for none.</param>
    /// <param name="objectives">Where to lead before exploring, lowest reachable tier first; the caller drops the ones reached or done. Null for none.</param>
    public GuideStep Step(
        PlayerInput player, bool exitMissing = false, IReadOnlyList<GuideDestination>? destinations = null, IReadOnlyList<GuideDestination>? objectives = null)
    {
        if (_map.TileAt(player.X, player.Y) is not { } tile) return GuideStep.Unknown;

        var graph = CurrentGraph();
        if (_beforeOpening is { } before)
        {
            _beforeOpening = null;
            NoteBehind(before, tile, player);
        }
        MarkSeenNeighbours(graph, tile, player);
        SpendReachedFrontiers(graph, player);
        if (exitMissing && _rechecking) MarkRechecked(graph, player);
        destinations ??= [];
        objectives ??= [];
        foreach (var d in destinations.Where(d => TileRoutes.Distance((player.X, player.Y), d.Position) <= DestinationArrivalUnits))
            _reachedDestinations.Add(d.Id);
        var destinationKey = string.Join(";", destinations.Concat(objectives).Select(d => $"{d.Id}:{d.Tier}")) + $"|{_reachedDestinations.Count}";

        var here = (player.X, player.Y);
        if (_cached is not null && _cachedVersion == _map.Version && _cachedBlocked == _blockedVersion && _cachedSpent == _spentFrontiers.Count &&
            _cachedRecheck == _recheckVersion && _cachedExitMissing == exitMissing && _cachedDestinations == destinationKey && _cachedJumps == _jumpsVersion &&
            TileRoutes.Distance(here, _cachedPlayer) <= RoomRouteCache.RecomputeThresholdUnits)
            return _cached;

        _cached = Compute(graph, tile, player, exitMissing, destinations, objectives);
        _cachedPlayer = here;
        _cachedVersion = _map.Version;
        _cachedBlocked = _blockedVersion;
        _cachedSpent = _spentFrontiers.Count;
        _cachedRecheck = _recheckVersion;
        _cachedExitMissing = exitMissing;
        _cachedDestinations = destinationKey;
        _cachedJumps = _jumpsVersion;
        return _cached;
    }

    private TileGraph CurrentGraph()
    {
        if (_graph is null || _graphVersion != _map.Version || _graphBlocked != _blockedVersion)
        {
            _graph = TileGraph.Build(_map, _walkability, _blocked);
            _graphVersion = _map.Version;
            _graphBlocked = _blockedVersion;
        }
        return _graph;
    }

    private GuideStep Compute(
        TileGraph graph, MapTile tile, PlayerInput player, bool exitMissing, IReadOnlyList<GuideDestination> destinations,
        IReadOnlyList<GuideDestination> objectives)
    {
        var here = (player.X, player.Y);
        var start = graph.NodeAt(tile, here)!;
        var routes = TileRoutes.From(start, here);

        var objectiveCandidates = TierCandidates(graph, routes, objectives, GuideTargetKind.Objective, _ => true);
        var exploring = Candidates(routes, start).ToList();
        if (_current is { } previous && objectiveCandidates.Concat(exploring).All(c => c.Target.Id != previous.Target.Id) && Reached(previous, here))
            Arrivals++;
        exploring = PastOpeningFirst(exploring);
        // An objective known beyond the explored map (the game's map marks it): explore the way towards it, as a sighted player would.
        var waiting = objectiveCandidates.Count > 0 ? [] : Waiting(graph, routes, objectives);
        if (waiting.Count > 0)
            exploring = [.. exploring.Select(c => c with { Cost = c.Cost + waiting.Min(w => TileRoutes.Distance(c.Target.Position, w.Position)) })];
        var candidates = objectiveCandidates.Count > 0 ? objectiveCandidates : exploring;
        _rechecking = false;
        if (candidates.Count == 0 && exitMissing)
        {
            candidates = RecheckCandidates(routes, player);
            _rechecking = candidates.Count > 0;
        }
        if (candidates.Count == 0)
            candidates = TierCandidates(graph, routes, destinations, GuideTargetKind.Destination, d => !_reachedDestinations.Contains(d.Id));
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
        var status = _rechecking ? GuideStatus.Rechecking : GuideStatus.Guiding;
        var waypoint = Waypoint(hops, chosen, player, out var remaining, out var jumpAt);
        var toward = waiting.Count > 0 && exploring.Contains(chosen) ? waiting.MinBy(w => TileRoutes.Distance(chosen.Target.Position, w.Position)) : null;
        return new GuideStep(status, chosen.Target, waypoint, remaining, Arrivals, _rechecking ? _round : 0, toward, jumpAt);
    }

    // A dungeon goes on through the door it just unlocked: the ground a barrier held back leads, the ground left behind waits
    // until none of it is left (tester, Derelict Lodge, 2026-10-16: the guide turned back to the first area's corners).
    private void NoteBehind(BlockedCells before, MapTile tile, PlayerInput player)
    {
        var graph = TileGraph.Build(_map, _walkability, before);
        if (graph.NodeAt(tile, (player.X, player.Y)) is not { } start) return;
        var routes = TileRoutes.From(start, (player.X, player.Y));
        _behindTiles = [.. routes.Reached.Select(n => TileGraph.Key(n.Tile))];
        _behindFrontiers = [.. routes.Reached.SelectMany(n => n.Hops.Where(h => h.To is null).Select(h => FrontierId(n, h.Opening)))];
    }

    private List<Candidate> PastOpeningFirst(List<Candidate> exploring)
    {
        if (_behindTiles.Count == 0) return exploring;
        var past = exploring.Where(c => c.FrontierHop is null
            ? !_behindTiles.Contains(TileGraph.Key(c.Node.Tile)) : !_behindFrontiers.Contains(c.Target.Id)).ToList();
        if (past.Count > 0) return past;
        _behindTiles = [];
        _behindFrontiers = [];
        return exploring;
    }

    // The located objectives of the lowest tier among those the routes can't reach yet: on a tile not loaded, or cut off.
    private List<GuideDestination> Waiting(TileGraph graph, TileRoutes routes, IReadOnlyList<GuideDestination> objectives)
    {
        var waiting = objectives.Where(d => d.Located && (_map.TileAt(d.Position.X, d.Position.Y) is not { } tile
            || graph.NodeAt(tile, d.Position) is not { } node || !routes.Reaches(node))).ToList();
        if (waiting.Count == 0) return waiting;
        int tier = waiting.Min(d => d.Tier);
        return [.. waiting.Where(d => d.Tier == tier)];
    }

    // A target left the candidate list because the player got there, not because its far side merely loaded.
    private bool Reached(Candidate target, (float X, float Y) player) => target.Target.Kind switch
    {
        GuideTargetKind.ExitCandidate => false, // counted when passed, see MarkRechecked
        GuideTargetKind.Destination => _reachedDestinations.Contains(target.Target.Id),
        GuideTargetKind.Objective => TileRoutes.Distance(player, target.Target.Position) <= MathF.Max(target.Reach, DestinationArrivalUnits),
        _ => target.FrontierHop is null ? _map.IsVisited(target.Node.Tile) : _spentFrontiers.Contains(target.Target.Id),
    };

    // The open points of the lowest reachable tier: once every one of them is closed the tier ends, it never falls back a
    // tier (a closed point still holds its tier). A point on an unloaded or cut-off tile waits.
    private List<Candidate> TierCandidates(
        TileGraph graph, TileRoutes routes, IReadOnlyList<GuideDestination> points, GuideTargetKind kind, Func<GuideDestination, bool> open)
    {
        var reachable = new List<(GuideDestination Destination, TileNode Node)>();
        foreach (var d in points)
            if (_map.TileAt(d.Position.X, d.Position.Y) is { } tile && graph.NodeAt(tile, d.Position) is { } node && routes.Reaches(node))
                reachable.Add((d, node));
        if (reachable.Count == 0) return [];
        int tier = reachable.Min(r => r.Destination.Tier);
        return [.. reachable.Where(r => r.Destination.Tier == tier && open(r.Destination)).Select(r => new Candidate(
            new GuideTarget(kind, r.Destination.Id, r.Destination.Position),
            routes.LengthTo(r.Node) + TileRoutes.Distance(routes.EntryPoint(r.Node), r.Destination.Position), r.Node, null, r.Destination.Radius))];
    }

    private IEnumerable<Candidate> Candidates(TileRoutes routes, TileNode start)
    {
        var nearest = NearestPerTile(routes).ToHashSet();
        foreach (var node in routes.Reached)
        {
            if (!_map.IsVisited(node.Tile))
            {
                if (node.Tile != start.Tile && nearest.Contains(node))
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
        var all = NearestPerTile(routes).Where(n => _isExitCandidate(n.Tile.Input.SceneSno)).ToList();
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

    // One reached node per tile: a tile split by a barrier counts once, through its nearest reached region.
    private static IEnumerable<TileNode> NearestPerTile(TileRoutes routes) =>
        routes.Reached.GroupBy(n => n.Tile).Select(g => g.MinBy(routes.LengthTo)!);

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

    // Heads for the first opening that leaves the player's room; a target tile inside the room is the route's last hop, and a
    // point target there (an objective, a portal) is walked to over the room's floor.
    private (float X, float Y) Waypoint(List<RouteHop> hops, Candidate chosen, PlayerInput player, out float remaining, out (float X, float Y)? jumpAt)
    {
        float total = chosen.Cost;
        remaining = total;
        jumpAt = null;
        bool point = chosen.FrontierHop is null && chosen.Target.Kind is GuideTargetKind.Objective or GuideTargetKind.Destination;
        if (hops.Count == 0 && !point) return chosen.Target.Position; // the target is in the player's tile
        var room = RoomModel.TryGetRoom(_map, player.X, player.Y, out var found, _walkability, _blocked) ? found : null;
        var roomTiles = room?.Tiles.Select(TileGraph.Key).ToHashSet() ?? [];
        if (point && room is not null && hops.All(h => h.To is { } next && roomTiles.Contains(TileGraph.Key(next.Tile))))
        {
            float reach = chosen.Reach > 0 ? chosen.Reach : DestinationArrivalUnits;
            var near = RoomRouteCache.TraceToPoint(room, (player.X, player.Y), chosen.Target.Position, reach, _jumps);
            if (!near.UsedFallback)
            {
                remaining = near.Length;
                jumpAt = near.JumpAt;
                return near.Point;
            }
        }
        if (hops.Count == 0) return chosen.Target.Position;

        var hop = hops.FirstOrDefault(h => h.To is null || !roomTiles.Contains(TileGraph.Key(h.To.Tile))) ?? hops[^1];
        if (room is null) return hop.Opening.Position;

        var trace = RoomRouteCache.Trace(room, (player.X, player.Y), hop.Opening, _jumps);
        remaining = trace.UsedFallback ? total : trace.Length + (total - hop.LengthSoFar);
        jumpAt = trace.JumpAt;
        return trace.Point;
    }

    // A sighted player reads a dead end from its doorway: a tile joined to the player's by a real opening (never across
    // a wall) is explored once the player is within SeenRadiusUnits of its centre. Big tiles still need entering.
    private void MarkSeenNeighbours(TileGraph graph, MapTile tile, PlayerInput player)
    {
        if (graph.NodeAt(tile, (player.X, player.Y)) is not { } node) return;
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
