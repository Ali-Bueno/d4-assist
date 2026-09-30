namespace D4Assist.Navigation;

/// <summary>One crossing on a route: the opening taken, the tile entered (null for a frontier) and the length walked up to it.</summary>
internal sealed record RouteHop(RoomOpening Opening, TileNode? To, float LengthSoFar);

/// <summary>
/// Shortest route lengths from the player's tile over a <see cref="TileGraph"/> (Dijkstra). Legs are straight
/// lines between crossing points; each tile keeps the crossing it was first reached through.
/// </summary>
internal sealed class TileRoutes
{
    private sealed record Arrival(float Length, (float X, float Y) At, TileNode? From, TileHop? Via);

    private readonly Dictionary<TileNode, Arrival> _arrivals = [];

    public IEnumerable<TileNode> Reached => _arrivals.Keys;

    public bool Reaches(TileNode node) => _arrivals.ContainsKey(node);

    public float LengthTo(TileNode node) => _arrivals[node].Length;

    /// <summary>The point where the shortest route enters <paramref name="node"/> (the player's own position for the start).</summary>
    public (float X, float Y) EntryPoint(TileNode node) => _arrivals[node].At;

    public static TileRoutes From(TileNode start, (float X, float Y) player)
    {
        var routes = new TileRoutes();
        routes._arrivals[start] = new Arrival(0f, player, null, null);
        var queue = new PriorityQueue<TileNode, float>();
        queue.Enqueue(start, 0f);
        var settled = new HashSet<TileNode>();
        while (queue.TryDequeue(out var node, out _))
        {
            if (!settled.Add(node)) continue;
            var here = routes._arrivals[node];
            foreach (var hop in node.Hops)
            {
                if (hop.To is not { } next) continue;
                var length = here.Length + Distance(here.At, hop.Opening.Position);
                if (routes._arrivals.TryGetValue(next, out var known) && known.Length <= length) continue;
                routes._arrivals[next] = new Arrival(length, hop.Opening.Position, node, hop);
                queue.Enqueue(next, length);
            }
        }
        return routes;
    }

    /// <summary>The crossings from the start to <paramref name="node"/>, in walking order (empty for the start).</summary>
    public List<RouteHop> HopsTo(TileNode node)
    {
        var hops = new List<RouteHop>();
        for (var arrival = _arrivals[node]; arrival.Via is { } via; arrival = _arrivals[arrival.From!])
            hops.Add(new RouteHop(via.Opening, via.To, arrival.Length));
        hops.Reverse();
        return hops;
    }

    public static float Distance((float X, float Y) a, (float X, float Y) b) =>
        MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2));
}
