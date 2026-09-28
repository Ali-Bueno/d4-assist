using D4Assist.Radar;

namespace D4Assist.Radar.Tests;

/// <summary>An IRadarWorld from two functions of the cell (x = engine X/south, y = engine Y/east).</summary>
internal sealed class FakeRadarWorld(Func<int, int, RadarCell> cell, Func<int, int, int>? room = null) : IRadarWorld
{
    public HashSet<int> Visited { get; } = [];
    public double TypicalOpeningWidth { get; set; }

    public RadarCell Cell(int x, int y) => cell(x, y);
    public int RoomAt(int x, int y) => room?.Invoke(x, y) ?? 0;
    public bool IsVisited(int r) => Visited.Contains(r);

    /// <summary>A three-cell corridor along +X (y = 1..3) from x = 0 up to a wall at <paramref name="wallX"/>;
    /// everything else is wall. Cells from <paramref name="unknownFromX"/> on are unloaded, and cells from
    /// <paramref name="secondRoomFromX"/> on belong to room 1.</summary>
    public static FakeRadarWorld Corridor(int wallX = 40, int? unknownFromX = null, int? secondRoomFromX = null) => new(
        (x, y) => unknownFromX is { } u && x >= u ? RadarCell.Unknown
            : x >= 0 && x < wallX && y >= 1 && y <= 3 ? RadarCell.Open : RadarCell.Blocked,
        (x, _) => secondRoomFromX is { } r && x >= r ? 1 : 0);

    /// <summary>Open floor everywhere except where <paramref name="blocked"/> says so.</summary>
    public static FakeRadarWorld Field(Func<int, int, bool> blocked) =>
        new((x, y) => blocked(x, y) ? RadarCell.Blocked : RadarCell.Open);
}
