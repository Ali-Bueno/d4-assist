namespace D4Assist.Navigation;

/// <summary>A tile, or one floor region of a tile split by a barrier, with the openings that leave it; <see cref="To"/> is null for an opening into never-loaded space (a frontier).</summary>
internal sealed class TileNode(MapTile tile)
{
    public MapTile Tile { get; } = tile;
    public List<TileHop> Hops { get; } = [];
}

/// <param name="Extra">Cost on top of the walk to the opening: a traversal's use and the props buried around it.</param>
/// <param name="Traversal">A traversal between two floor regions of one tile; <see cref="Opening"/> sits where it lands.</param>
internal readonly record struct TileHop(RoomOpening Opening, TileNode? To, float Extra = 0f, bool Traversal = false);

/// <summary>
/// The known tiles as a graph: nodes are tiles, edges are their exit openings (real walkable gaps, via
/// OpeningFinder) that land on another known tile. An opening found from one side also leads back from the
/// other where that tile has floor at it: a big preset room's mask can omit the sides its neighbours open into. Touching tiles of one room (RoomModel) are linked without an exit bit, because preset
/// rooms' live exit masks can omit their internal sides (research/client-memory.md §1), but only where both walk maps
/// have floor at their shared edge: the Skovos docks are one room, water parts two of its tiles, and the guide led to the
/// far one across it (live 2026-10-17). A tile that live barriers split, or one a standing prop buries a traversal of, is one
/// node per floor region (TileRegions), joined where its floor meets the crossing and by its traversals, but for the closed ones.
/// </summary>
internal sealed class TileGraph
{
    private const float HalfCell = BlockedCells.CellSizeUnits / 2f;

    /// <summary>How far in from a shared edge floor still counts as reaching it: a walk map can stop short of its tile's edge.</summary>
    public const float EdgeFloorUnits = 3f;

    private readonly Dictionary<(int, int), TileRegions> _tiles = [];

    public IEnumerable<TileNode> Nodes => _tiles.Values.SelectMany(t => t.Nodes);

    /// <summary>The node of <paramref name="tile"/> holding <paramref name="point"/>, or nearest to it when a barrier splits the tile.</summary>
    public TileNode? NodeAt(MapTile tile, (float X, float Y) point) => _tiles.GetValueOrDefault(Key(tile))?.At(point);

    public static (int, int) Key(MapTile tile) => (tile.CellX, tile.CellY);

    /// <param name="traversals">The loaded tiles' traversals: those joining two regions of a split tile become hops, and a tile
    /// holding a buried one's end is split into its floor regions. Null for none.</param>
    public static TileGraph Build(
        DungeonMap map, ITileWalkability? walkability, BlockedCells? blocked = null, IReadOnlyList<GraphTraversal>? traversals = null)
    {
        blocked ??= BlockedCells.None;
        traversals ??= [];
        var buriedTiles = traversals.Where(t => t.Splits)
            .SelectMany(t => new[] { map.TileAt(t.Link.A.X, t.Link.A.Y), map.TileAt(t.Link.B.X, t.Link.B.Y) })
            .OfType<MapTile>().Select(Key).ToHashSet();
        var graph = new TileGraph();
        foreach (var tile in map.Tiles) graph._tiles[Key(tile)] = TileRegions.Build(tile, walkability, blocked, buriedTiles.Contains(Key(tile)));

        foreach (var near in graph._tiles.Values)
            foreach (var opening in OpeningFinder.Find(map, [near.Tile], [near.Tile], walkability, blocked))
            {
                var probe = OpeningFinder.Probe(opening.Side, opening.Position);
                var neighbour = opening.Unexplored ? null : map.TileAt(probe.X, probe.Y);
                var far = neighbour is null ? null : graph._tiles.GetValueOrDefault(Key(neighbour));
                // The Labyrinthhain's entrance tile has floor along its west edge at y 8-14 and 20-26; beyond the first run lie only
                // two stray floor cells, and the guide led into the trees there (live 2026-10-21). The far side's own opening
                // comes back as a reverse hop below. A filler beyond is rock whatever floor reaches the edge on this side: the
                // Darkened Holt's first room has a 3-unit gap at (-4.5,-32) into Frac_Pine_FILLER_01, and the guide led a
                // tester against it for a minute and a half (log of 2026-11-14).
                if (far is not null && (IsSolid(far.Tile, walkability) || !FloorReaches(far.Tile, opening, walkability, FloorRunCells))) continue;
                foreach (var (from, to, crossing) in Contacts(near, far, opening))
                    from.Hops.Add(new TileHop(crossing, to));
            }

        // A big preset room's live mask can leave out sides its neighbours open into: Rathma's golem chamber (64x64, mask S+E)
        // was entered from the west and north, and the guide, standing in it, found every way out of it closed (live 2026-10-20).
        var reverse = graph.Nodes
            .SelectMany(from => from.Hops.Where(h => h.To is { } to && !to.Hops.Any(back => back.To == from)
                    && FloorReaches(to.Tile, h.Opening, walkability))
                .Select(h => (From: h.To!, Hop: new TileHop(h.Opening with { Side = Opposite(h.Opening.Side) }, from))))
            .ToList();
        foreach (var (from, hop) in reverse) from.Hops.Add(hop);

        foreach (var room in graph._tiles.Values.GroupBy(t => t.Tile.Input.NodeId))
            foreach (var near in room)
                foreach (var far in room)
                    if (near != far && RoomModel.Adjacent(near.Tile, far.Tile) && !IsSolid(near.Tile, walkability) && !IsSolid(far.Tile, walkability)
                        && FloorMeets(near.Tile, far.Tile, walkability))
                        foreach (var (from, to, crossing) in Contacts(near, far, SharedEdge(near.Tile, far.Tile)))
                            if (!from.Hops.Any(h => h.To == to)) from.Hops.Add(new TileHop(crossing, to));

        foreach (var traversal in traversals) graph.Join(map, traversal);
        return graph;
    }

    // A traversal between two regions of a split tile: without it a ladder's platform would be reached only through other tiles.
    private void Join(DungeonMap map, GraphTraversal traversal)
    {
        var (a, b) = (traversal.Link.A, traversal.Link.B);
        if (traversal.Closed || map.TileAt(a.X, a.Y) is not { } tile || map.TileAt(b.X, b.Y) is not { } other || Key(tile) != Key(other)
            || _tiles[Key(tile)] is not { IsSplit: true } regions) return;
        var (from, to) = (regions.At(a), regions.At(b));
        if (from == to) return;
        float extra = RoomPathfinder.JumpExtraCost + traversal.PropCost;
        from.Hops.Add(new TileHop(new RoomOpening(Facing(a, b), b, Unexplored: false), to, extra, Traversal: true));
        if (!traversal.Link.OneWay) to.Hops.Add(new TileHop(new RoomOpening(Facing(b, a), a, Unexplored: false), from, extra, Traversal: true));
    }

    // The side a leap from one end towards the other crosses (world axes, ExitMaskBits: north is -X, east +Y).
    private static ExitSide Facing((float X, float Y) from, (float X, float Y) to) =>
        MathF.Abs(to.X - from.X) >= MathF.Abs(to.Y - from.Y)
            ? to.X < from.X ? ExitSide.North : ExitSide.South
            : to.Y > from.Y ? ExitSide.East : ExitSide.West;

    // The nodes a crossing joins (far null: a frontier). Unsplit tiles meet across the whole crossing; with a split side each
    // cell along it pairs the regions on either side, a barrier cell pairs nothing, and every pair crosses at its own part.
    private static IEnumerable<(TileNode From, TileNode? To, RoomOpening Crossing)> Contacts(TileRegions near, TileRegions? far, RoomOpening crossing)
    {
        if (!near.IsSplit && far is not { IsSplit: true })
        {
            yield return (near.Nodes[0], far?.Nodes[0], crossing);
            yield break;
        }

        var normal = ExitMaskBits.Normal(crossing.Side);
        var along = (MathF.Abs(normal.Y), MathF.Abs(normal.X));
        int count = Math.Max(1, (int)MathF.Round(crossing.Width / BlockedCells.CellSizeUnits));
        var points = Enumerable.Range(0, count)
            .Select(i => Offset(crossing.Position, along, (i + 0.5f) * crossing.Width / count - crossing.Width / 2f)).ToList();
        var froms = near.Along([.. points.Select(p => Offset(p, normal, -HalfCell))], Offset(crossing.Position, normal, -HalfCell));
        var tos = far?.Along([.. points.Select(p => Offset(p, normal, HalfCell))], Offset(crossing.Position, normal, HalfCell));
        var pairs = Enumerable.Range(0, count)
            .Where(i => froms[i] is not null && (tos is null || tos[i] is not null))
            .GroupBy(i => (From: froms[i]!, To: tos?[i]))
            .ToList();
        foreach (var pair in pairs)
            yield return (pair.Key.From, pair.Key.To, pairs.Count == 1 ? crossing : crossing with
            {
                Position = (pair.Average(i => points[i].X), pair.Average(i => points[i].Y)),
                Width = pair.Count() * crossing.Width / count,
            });
    }

    private static (float X, float Y) Offset((float X, float Y) point, (float X, float Y) direction, float distance) =>
        (point.X + direction.X * distance, point.Y + direction.Y * distance);

    // A filler tile shares its neighbours' room node, and with no walk map nothing else says it has no floor (research/dungeon-data.md §4.1e).
    private static bool IsSolid(MapTile tile, ITileWalkability? walkability) =>
        tile.Input.ExitMask == 0 && tile.Input.AuthoredExitMask == 0 && walkability is not null && !walkability.TryGet(tile.Input.SceneSno, out _);

    // Somewhere along the shared edge both tiles have floor within EdgeFloorUnits of it; true when a walk map is unknown.
    private static bool FloorMeets(MapTile near, MapTile far, ITileWalkability? walkability)
    {
        if (walkability is null || !walkability.TryGet(near.Input.SceneSno, out var nearGrid) || !walkability.TryGet(far.Input.SceneSno, out var farGrid))
            return true;
        int nearTurns = TileRotation.QuarterTurns(near.Input.YawDegrees), farTurns = TileRotation.QuarterTurns(far.Input.YawDegrees);
        if (nearTurns < 0 || farTurns < 0) return true;
        var edge = SharedEdge(near, far);
        var normal = ExitMaskBits.Normal(edge.Side);
        var along = (MathF.Abs(normal.Y), MathF.Abs(normal.X));
        for (var a = -edge.Width / 2f + HalfCell; a < edge.Width / 2f; a += 1f)
        {
            var point = Offset(edge.Position, along, a);
            if (HasFloorNear(nearGrid, near.Input, nearTurns, point, normal, -1f) && HasFloorNear(farGrid, far.Input, farTurns, point, normal, 1f))
                return true;
        }
        return false;
    }

    /// <summary>Points in a row along an opening the tile beyond must have floor at: one stray floor cell is no way through.</summary>
    public const int FloorRunCells = 2;

    // Somewhere along a crossing found from the other side, the tile beyond has floor within EdgeFloorUnits of it at
    // <paramref name="run"/> points in a row (fewer on a narrower crossing); true when a walk map is unknown.
    private static bool FloorReaches(MapTile tile, RoomOpening crossing, ITileWalkability? walkability, int run = 1)
    {
        if (walkability is null || !walkability.TryGet(tile.Input.SceneSno, out var grid)) return true;
        int turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
        if (turns < 0) return true;
        var normal = ExitMaskBits.Normal(crossing.Side);
        var along = (MathF.Abs(normal.Y), MathF.Abs(normal.X));
        float width = MathF.Max(crossing.Width, 1f);
        run = Math.Clamp((int)width, 1, run);
        int inRow = 0;
        for (var a = -width / 2f + HalfCell; a < width / 2f; a += 1f)
        {
            inRow = HasFloorNear(grid, tile.Input, turns, Offset(crossing.Position, along, a), normal, 1f) ? inRow + 1 : 0;
            if (inRow >= run) return true;
        }
        return false;
    }

    private static ExitSide Opposite(ExitSide side) => side switch
    {
        ExitSide.North => ExitSide.South,
        ExitSide.South => ExitSide.North,
        ExitSide.East => ExitSide.West,
        _ => ExitSide.East,
    };

    // Floor within EdgeFloorUnits of the edge point, stepping into the tile (sign -1: against the normal, into the near tile).
    private static bool HasFloorNear(WalkGrid grid, TileInput tile, int turns, (float X, float Y) edgePoint, (float X, float Y) normal, float sign)
    {
        for (var d = HalfCell; d <= EdgeFloorUnits; d += grid.Resolution)
        {
            var local = TileRotation.ToLocal(tile, Offset(edgePoint, normal, sign * d), turns);
            var (row, col) = grid.CellAt(local.X, local.Y);
            if (grid.IsWalkable(row, col)) return true;
        }
        return false;
    }

    // Crossing between two touching tiles of one room: the middle of their shared edge, as wide as the edge they share.
    private static RoomOpening SharedEdge(MapTile from, MapTile to)
    {
        var a = from.Input;
        var b = to.Input;
        if (b.OriginX >= a.OriginX + a.SizeX || a.OriginX >= b.OriginX + b.SizeX)
        {
            bool south = b.OriginX >= a.OriginX + a.SizeX;
            float low = MathF.Max(a.OriginY, b.OriginY), high = MathF.Min(a.OriginY + a.SizeY, b.OriginY + b.SizeY);
            return new RoomOpening(south ? ExitSide.South : ExitSide.North, (south ? b.OriginX : a.OriginX, (low + high) / 2f), Unexplored: false, high - low);
        }
        bool east = b.OriginY >= a.OriginY + a.SizeY;
        float left = MathF.Max(a.OriginX, b.OriginX), right = MathF.Min(a.OriginX + a.SizeX, b.OriginX + b.SizeX);
        return new RoomOpening(east ? ExitSide.East : ExitSide.West, ((left + right) / 2f, east ? b.OriginY : a.OriginY), Unexplored: false, right - left);
    }
}
