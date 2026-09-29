namespace D4Assist.Navigation;

/// <summary>
/// Groups <see cref="DungeonMap"/> tiles into rooms and finds each room's openings.
/// Rooms = tiles sharing a NodeId (D4's group-id equivalent of D2R's one-room-per-
/// streamed-chamber, research/d2r-radar-port.md §1). Grouping additionally requires
/// grid adjacency: NodeId can default to the same value on unrelated tiles, and two
/// same-id tiles that don't actually touch must not merge into one room. A tile with
/// no adjacent same-id neighbour falls back to being its own single-tile room.
/// </summary>
public static class RoomModel
{
    /// <summary>The room covering a world position, or false if no loaded tile covers it.</summary>
    public static bool TryGetRoom(DungeonMap map, float worldX, float worldY, out Room room, ITileWalkability? walkability = null)
    {
        var tile = map.TileAt(worldX, worldY);
        if (tile is null) { room = null!; return false; }
        room = BuildRoomAt(map, tile, walkability);
        return true;
    }

    /// <summary>Every room formed by the tiles currently in <paramref name="map"/>.</summary>
    public static IReadOnlyList<Room> BuildAll(DungeonMap map, ITileWalkability? walkability = null)
    {
        var visited = new HashSet<MapTile>();
        var rooms = new List<Room>();
        foreach (var tile in map.Tiles)
        {
            if (visited.Contains(tile)) continue;
            rooms.Add(BuildRoomAt(map, tile, walkability, visited));
        }
        return rooms;
    }

    private static Room BuildRoomAt(DungeonMap map, MapTile seed, ITileWalkability? walkability, HashSet<MapTile>? visited = null)
    {
        var sameNode = map.Tiles.Where(t => t.Input.NodeId == seed.Input.NodeId).ToList();
        var members = CollectComponent(seed, sameNode, visited ?? new HashSet<MapTile>());
        var memberSet = new HashSet<MapTile>(members);

        var anchor = members.OrderBy(t => t.Input.OriginX).ThenBy(t => t.Input.OriginY).First();
        var key = new RoomKey(anchor.Input.OriginX, anchor.Input.OriginY, true);

        var openings = OpeningFinder.Find(map, members, memberSet, walkability);
        return new Room(key, members, openings, RoomWalkGrid.Build(members, walkability));
    }

    // BFS over same-NodeId tiles, connecting only tiles whose footprints actually touch.
    private static List<MapTile> CollectComponent(MapTile seed, List<MapTile> sameNode, HashSet<MapTile> visited)
    {
        var component = new List<MapTile>();
        var queue = new Queue<MapTile>();
        queue.Enqueue(seed);
        visited.Add(seed);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            component.Add(current);
            foreach (var other in sameNode)
            {
                if (visited.Contains(other) || !Adjacent(current, other)) continue;
                visited.Add(other);
                queue.Enqueue(other);
            }
        }
        return component;
    }

    // Two tiles are grid-adjacent when their footprints share an edge (touch on one
    // axis, overlap on the other) — axis-aligned rectangle adjacency.
    internal static bool Adjacent(MapTile a, MapTile b)
    {
        var aRight = a.CellX + a.SpanCellsX;
        var bRight = b.CellX + b.SpanCellsX;
        var aBottom = a.CellY + a.SpanCellsY;
        var bBottom = b.CellY + b.SpanCellsY;

        var touchesX = aRight == b.CellX || bRight == a.CellX;
        var overlapsY = a.CellY < bBottom && b.CellY < aBottom;
        var touchesY = aBottom == b.CellY || bBottom == a.CellY;
        var overlapsX = a.CellX < bRight && b.CellX < aRight;

        return (touchesX && overlapsY) || (touchesY && overlapsX);
    }
}
