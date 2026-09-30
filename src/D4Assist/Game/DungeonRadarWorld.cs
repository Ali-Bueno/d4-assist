using D4Assist.Navigation;
using D4Assist.Radar;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The radar's world over every tile seen in the current dungeon: one combined walk grid (each tile's offline
/// walk map placed at its origin, rotated by its yaw as RoomWalkGrid does), RoomModel rooms under stable ids,
/// and the rooms the player has stood in. Rebuilt only when the tile set changes. Not thread-safe.
/// </summary>
public sealed class DungeonRadarWorld(ITileWalkability walkability) : IRadarWorld
{
    // One radar cell per offline walk-map cell (1 world unit).
    private const float CellSize = NavGridReader.MapResolution;

    private readonly DungeonMap _map = new();
    private readonly Dictionary<(int X, int Y), TileState> _tiles = new();
    private readonly Dictionary<(int X, int Y), int> _roomOfTile = new();
    private readonly HashSet<int> _visited = [];
    private int _nextRoomId;
    private int _minX, _minY, _width, _height;
    private RadarCell[] _cells = [];
    private int[] _rooms = [];
    private IReadOnlySet<(int X, int Y)> _closedDoors = new HashSet<(int X, int Y)>();

    public double TypicalOpeningWidth { get; private set; }

    public IReadOnlyList<RadarDoorway> Doorways { get; private set; } = [];

    public int TileCount => _tiles.Count;
    public int RoomCount { get; private set; }

    /// <summary>Tiles whose cells read Unknown: no walk map, or a yaw off the 90-degree grid.</summary>
    public int TilesWithoutWalkability { get; private set; }

    /// <summary>A closed door turns walkable floor into a Door cell; walls stay walls.</summary>
    public RadarCell Cell(int x, int y)
    {
        var cell = TryIndex(x, y, out var i) ? _cells[i] : RadarCell.Unknown;
        return cell == RadarCell.Open && _closedDoors.Contains((x, y)) ? RadarCell.Door : cell;
    }

    /// <summary>Replaces the closed-door cells (DoorObstacles), in radar cell coordinates.</summary>
    public void SetClosedDoors(IReadOnlySet<(int X, int Y)> cells) => _closedDoors = cells;

    public int RoomAt(int x, int y) => TryIndex(x, y, out var i) ? _rooms[i] : -1;

    public bool IsVisited(int room) => _visited.Contains(room);

    /// <summary>Discards every tile, room and visit. Call on world change.</summary>
    public void Reset()
    {
        _map.Reset();
        _tiles.Clear();
        _roomOfTile.Clear();
        _visited.Clear();
        _nextRoomId = 0;
        _width = _height = 0;
        _cells = [];
        _rooms = [];
        _closedDoors = new HashSet<(int X, int Y)>();
        Doorways = [];
        TypicalOpeningWidth = 0;
        RoomCount = TilesWithoutWalkability = 0;
    }

    /// <summary>Accumulates a loaded-window snapshot; returns true when the tile set changed and was rebuilt.</summary>
    public bool UpdateTiles(IReadOnlyList<TileState> tiles)
    {
        var changed = new List<TileInput>();
        foreach (var tile in tiles)
        {
            var key = TileKey(tile.OriginX, tile.OriginY);
            if (_tiles.TryGetValue(key, out var known) && known == tile) continue;
            _tiles[key] = tile;
            changed.Add(ToInput(tile));
        }
        if (changed.Count == 0) return false;
        _map.AddOrUpdate(changed);
        Rebuild();
        return true;
    }

    /// <summary>Marks the room under the player as visited (D2R's "visited" ring, docs/exploration.md).</summary>
    public void MarkVisited(float x, float y)
    {
        var room = RoomAt((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));
        if (room >= 0) _visited.Add(room);
    }

    private void Rebuild()
    {
        var rooms = RoomModel.BuildAll(_map, walkability);
        var roomOf = AssignRoomIds(rooms);
        RoomCount = rooms.Count;
        TypicalOpeningWidth = LowerMedian(rooms.SelectMany(r => r.Openings).Where(o => !o.UsedFallback).Select(o => (double)o.Width));

        var tiles = _map.Tiles.ToList();
        _minX = (int)MathF.Floor(tiles.Min(t => t.Input.OriginX) / CellSize);
        _minY = (int)MathF.Floor(tiles.Min(t => t.Input.OriginY) / CellSize);
        _width = (int)MathF.Ceiling(tiles.Max(t => t.Input.OriginX + t.Input.SizeX) / CellSize) - _minX;
        _height = (int)MathF.Ceiling(tiles.Max(t => t.Input.OriginY + t.Input.SizeY) / CellSize) - _minY;
        _cells = new RadarCell[_width * _height];
        _rooms = new int[_width * _height];
        Array.Fill(_rooms, -1);

        // Footprints first (room ids, and Blocked where a walk map exists), then walkable cells, so a
        // neighbour's footprint never covers walkable floor.
        var grids = new List<(MapTile Tile, WalkGrid Grid, int Turns)>();
        foreach (var tile in tiles)
        {
            var turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
            var hasGrid = walkability.TryGet(tile.Input.SceneSno, out var grid) && turns >= 0;
            if (hasGrid) grids.Add((tile, grid, turns));
            FillFootprint(tile, roomOf[tile], hasGrid ? RadarCell.Blocked : RadarCell.Unknown);
        }
        TilesWithoutWalkability = tiles.Count - grids.Count;
        foreach (var (tile, grid, turns) in grids)
            for (var r = 0; r < grid.Rows; r++)
                for (var c = 0; c < grid.Cols; c++)
                {
                    if (!grid.IsWalkable(r, c)) continue;
                    var (localX, localY) = grid.CellCenter(r, c);
                    var (rx, ry) = TileRotation.Rotate(localX, localY, turns);
                    var x = (int)MathF.Floor((tile.Input.OriginX + rx) / CellSize);
                    var y = (int)MathF.Floor((tile.Input.OriginY + ry) / CellSize);
                    if (!TryIndex(x, y, out var i)) continue;
                    _cells[i] = RadarCell.Open;
                    _rooms[i] = roomOf[tile];
                }
        Doorways = [.. rooms.SelectMany(r => r.Openings.Where(o => !o.UsedFallback && o.Width > 0).Select(o => Doorway(roomOf[r.Tiles[0]], o)))];
    }

    // The room beyond is read just past the doorway, inside the neighbouring tile.
    private RadarDoorway Doorway(int room, RoomOpening opening)
    {
        var (nx, ny) = ExitMaskBits.Normal(opening.Side);
        var (x, y) = (opening.Position.X + 1.5f * nx, opening.Position.Y + 1.5f * ny);
        var beyond = RoomAt((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));
        return new RadarDoorway(new RadarVector(opening.Position.X / CellSize, opening.Position.Y / CellSize), opening.Width / CellSize, room, beyond);
    }

    private void FillFootprint(MapTile tile, int room, RadarCell state)
    {
        var x0 = (int)MathF.Floor(tile.Input.OriginX / CellSize);
        var y0 = (int)MathF.Floor(tile.Input.OriginY / CellSize);
        var x1 = (int)MathF.Ceiling((tile.Input.OriginX + tile.Input.SizeX) / CellSize);
        var y1 = (int)MathF.Ceiling((tile.Input.OriginY + tile.Input.SizeY) / CellSize);
        for (var x = x0; x < x1; x++)
            for (var y = y0; y < y1; y++)
            {
                if (!TryIndex(x, y, out var i)) continue;
                _cells[i] = state;
                _rooms[i] = room;
            }
    }

    // Room ids survive rebuilds: a room keeps the lowest id its tiles already had, so the engine never sees a
    // room change the player did not walk, and visits carry over when rooms merge.
    private Dictionary<MapTile, int> AssignRoomIds(IReadOnlyList<Room> rooms)
    {
        var result = new Dictionary<MapTile, int>();
        var claimed = new HashSet<int>();
        foreach (var room in rooms)
        {
            var previous = room.Tiles
                .Select(t => _roomOfTile.TryGetValue((t.CellX, t.CellY), out var id) ? id : -1)
                .Where(id => id >= 0 && !claimed.Contains(id))
                .ToList();
            var roomId = previous.Count > 0 ? previous.Min() : _nextRoomId++;
            if (previous.Any(_visited.Contains)) _visited.Add(roomId);
            claimed.Add(roomId);
            foreach (var tile in room.Tiles)
            {
                _roomOfTile[(tile.CellX, tile.CellY)] = roomId;
                result[tile] = roomId;
            }
        }
        return result;
    }

    private bool TryIndex(int x, int y, out int index)
    {
        int dx = x - _minX, dy = y - _minY;
        index = dy * _width + dx;
        return dx >= 0 && dy >= 0 && dx < _width && dy < _height;
    }

    // D2R Rooms::TypicalOpeningWidth takes the lower median (rooms.cpp:86-99).
    private static double LowerMedian(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }

    private static (int X, int Y) TileKey(float originX, float originY) => (GridMath.CellIndex(originX), GridMath.CellIndex(originY));

    private static TileInput ToInput(TileState t) =>
        new(t.SceneSno, t.Name, t.OriginX, t.OriginY, t.SizeX, t.SizeY, t.ExitMask, t.NodeId, t.GroupId, t.YawDegrees);
}
