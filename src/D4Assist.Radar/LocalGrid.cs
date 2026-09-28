namespace D4Assist.Radar;

/// <summary>
/// A square window of the world around the player's cell, read once per tick: D2R's structural snapshot and
/// navigation grid (d2r-access src/radar_structure.cpp:75-92, navigation_grid.cpp:122-161). Point body: D4's
/// walk map is already per-position walkability, so cells are not dilated by a footprint. Coordinates are
/// relative to the player's cell.
/// </summary>
internal sealed class LocalGrid
{
    private readonly RadarCell[] _cells;
    private readonly int[] _rooms;

    public LocalGrid(IRadarWorld world, int playerX, int playerY, int radius)
    {
        Radius = radius;
        Side = 2 * radius + 1;
        _cells = new RadarCell[Side * Side];
        _rooms = new int[Side * Side];
        for (var y = 0; y < Side; y++)
            for (var x = 0; x < Side; x++)
            {
                var index = y * Side + x;
                _cells[index] = world.Cell(playerX - radius + x, playerY - radius + y);
                _rooms[index] = world.RoomAt(playerX - radius + x, playerY - radius + y);
            }
    }

    public int Radius { get; }
    public int Side { get; }

    public bool OriginKnown => Cell(0, 0) != RadarCell.Unknown;

    public bool Contains(int dx, int dy) => Math.Abs(dx) <= Radius && Math.Abs(dy) <= Radius;

    public int Index(int dx, int dy) => (dy + Radius) * Side + dx + Radius;

    /// <summary>Outside the window reads Unknown, as D2R's absent grid cell does.</summary>
    public RadarCell Cell(int dx, int dy) => Contains(dx, dy) ? _cells[Index(dx, dy)] : RadarCell.Unknown;

    public int Room(int dx, int dy) => Contains(dx, dy) ? _rooms[Index(dx, dy)] : -1;

    public RadarCell[] CopyCells() => (RadarCell[])_cells.Clone();

    /// <summary>Erases a blocked cell by window index (the clutter filter).</summary>
    public void Clear(int index) => _cells[index] = RadarCell.Open;
}
