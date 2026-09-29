namespace D4Assist.Navigation;

/// <summary>A tile with the openings that leave it; <see cref="To"/> is null for an opening into never-loaded space (a frontier).</summary>
internal sealed class TileNode(MapTile tile)
{
    public MapTile Tile { get; } = tile;
    public List<TileHop> Hops { get; } = [];
}

internal readonly record struct TileHop(RoomOpening Opening, TileNode? To);

/// <summary>
/// The known tiles as a graph: nodes are tiles, edges are their exit openings (real walkable gaps, via
/// OpeningFinder) that land on another known tile. Exit bits are assumed symmetric — only the side that
/// carries the bit gets an edge. Touching tiles of one room (RoomModel) are always linked, because preset
/// rooms' live exit masks can omit their internal sides (research/client-memory.md §1).
/// </summary>
internal sealed class TileGraph
{
    private readonly Dictionary<(int, int), TileNode> _nodes = [];

    public IReadOnlyCollection<TileNode> Nodes => _nodes.Values;

    public TileNode? NodeAt(MapTile tile) => _nodes.GetValueOrDefault(Key(tile));

    public static (int, int) Key(MapTile tile) => (tile.CellX, tile.CellY);

    public static TileGraph Build(DungeonMap map, ITileWalkability? walkability)
    {
        var graph = new TileGraph();
        foreach (var tile in map.Tiles) graph._nodes[Key(tile)] = new TileNode(tile);

        foreach (var node in graph._nodes.Values)
            foreach (var opening in OpeningFinder.Find(map, [node.Tile], [node.Tile], walkability))
            {
                var probe = OpeningFinder.Probe(opening.Side, opening.Position);
                var neighbour = opening.Unexplored ? null : map.TileAt(probe.X, probe.Y);
                node.Hops.Add(new TileHop(opening, neighbour is null ? null : graph.NodeAt(neighbour)));
            }

        foreach (var room in graph._nodes.Values.GroupBy(n => n.Tile.Input.NodeId))
            foreach (var from in room)
                foreach (var to in room)
                    if (from != to && RoomModel.Adjacent(from.Tile, to.Tile) && !from.Hops.Any(h => h.To == to))
                        from.Hops.Add(new TileHop(SharedEdge(from.Tile, to.Tile), to));
        return graph;
    }

    // Crossing between two touching tiles of one room: the middle of their shared edge.
    private static RoomOpening SharedEdge(MapTile from, MapTile to)
    {
        var a = from.Input;
        var b = to.Input;
        if (b.OriginX >= a.OriginX + a.SizeX || a.OriginX >= b.OriginX + b.SizeX)
        {
            bool south = b.OriginX >= a.OriginX + a.SizeX;
            float y = (MathF.Max(a.OriginY, b.OriginY) + MathF.Min(a.OriginY + a.SizeY, b.OriginY + b.SizeY)) / 2f;
            return new RoomOpening(south ? ExitSide.South : ExitSide.North, (south ? b.OriginX : a.OriginX, y), Unexplored: false);
        }
        bool east = b.OriginY >= a.OriginY + a.SizeY;
        float x = (MathF.Max(a.OriginX, b.OriginX) + MathF.Min(a.OriginX + a.SizeX, b.OriginX + b.SizeX)) / 2f;
        return new RoomOpening(east ? ExitSide.East : ExitSide.West, (x, east ? b.OriginY : a.OriginY), Unexplored: false);
    }
}
