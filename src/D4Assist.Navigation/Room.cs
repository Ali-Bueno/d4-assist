namespace D4Assist.Navigation;

/// <summary>
/// A room: one or more tiles sharing a DRLG node id. Mirrors D2R's `Rooms::Room`
/// (rooms.h:23-28), whose single native room rect becomes a tile group here.
/// </summary>
public sealed class Room
{
    public Room(RoomKey key, IReadOnlyList<MapTile> tiles, IReadOnlyList<RoomOpening> openings, WalkGrid? navGrid = null)
    {
        Key = key;
        Tiles = tiles;
        Openings = openings;
        NavGrid = navGrid;
    }

    public RoomKey Key { get; }
    public IReadOnlyList<MapTile> Tiles { get; }
    public IReadOnlyList<RoomOpening> Openings { get; }

    /// <summary>Combined walkable grid over every member tile, in world coordinates, for path-based
    /// opening directions (RoomPathfinder). Null when no tile had walkability data.</summary>
    public WalkGrid? NavGrid { get; }

    /// <summary>A room spanning more than one tile (e.g. the observed 2x2, node 0xD5/group 0x14D).</summary>
    public bool IsMultiTile => Tiles.Count > 1;

    public bool Contains(float worldX, float worldY)
    {
        foreach (var tile in Tiles)
            if (tile.Contains(worldX, worldY)) return true;
        return false;
    }

    /// <summary>True when (worldX, worldY) lies within <paramref name="margin"/> of any member tile's
    /// footprint — spatial stickiness so briefly stepping past a room's edge doesn't flip rooms.</summary>
    public bool IsNear(float worldX, float worldY, float margin)
    {
        foreach (var tile in Tiles)
        {
            var t = tile.Input;
            if (worldX >= t.OriginX - margin && worldX < t.OriginX + t.SizeX + margin &&
                worldY >= t.OriginY - margin && worldY < t.OriginY + t.SizeY + margin)
                return true;
        }
        return false;
    }
}
