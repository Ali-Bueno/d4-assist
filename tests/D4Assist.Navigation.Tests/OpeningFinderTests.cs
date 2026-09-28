using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

/// <summary>
/// Covers the room-announcement fix: openings come from walkable runs (OpeningFinder), not the tile
/// edge's geometric midpoint. The off-centre case reuses TT_Step_City_to_Protodun_Jail_NSEW_01's real
/// run ranges (see WalkGridOrientationTests) — the exact tile from the 2026-09-28 live bug report.
/// </summary>
public class OpeningFinderTests
{
    private static WalkGrid JailTileGrid()
    {
        var cells = new bool[64, 64];
        foreach (var c in Enumerable.Range(10, 13)) cells[0, c] = true;
        foreach (var c in Enumerable.Range(40, 11)) cells[63, c] = true;
        foreach (var r in Enumerable.Range(9, 15)) cells[r, 0] = true;
        cells[4, 0] = true; // isolated 1-cell run: narrower than the player, must be filtered
        foreach (var r in Enumerable.Range(39, 16)) cells[r, 63] = true;
        return new WalkGrid(1f, 0f, 0f, 64, 64, cells);
    }

    private static (MapTile Tile, DungeonMap Map, HashSet<MapTile> Members) SingleTileRoom(TileInput input)
    {
        var map = new DungeonMap();
        map.AddOrUpdate([input]);
        var tile = map.Tiles.Single();
        return (tile, map, new HashSet<MapTile> { tile });
    }

    [Fact]
    public void Off_centre_openings_on_a_64_side_land_on_the_real_run_not_the_edge_midpoint()
    {
        var input = new TileInput(1, "jail", 64, 32, 64, 64, ExitMaskBits.North | ExitMaskBits.South | ExitMaskBits.East | ExitMaskBits.West, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var walkability = new FakeTileWalkability().Add(1, JailTileGrid());

        var openings = OpeningFinder.Find(map, [tile], members, walkability);

        Assert.Equal(4, openings.Count); // the isolated 1-cell W run must not count as a 5th
        Assert.All(openings, o => Assert.False(o.UsedFallback));

        var south = Assert.Single(openings, o => o.Side == ExitSide.South);
        Assert.Equal((128f, 77.5f), south.Position);
        Assert.Equal(11f, south.Width);

        // The bug: the naive edge midpoint (128, 64) reads as "down-left" from the tile centre; the
        // real off-centre run (128, 77.5) reads as "down" — a different, correct, spoken direction.
        var player = (X: 96f, Y: 64f);
        var naiveMidpoint = ExitMaskBits.EdgeMidpoint(input, ExitSide.South);
        Assert.Equal(StickDirection.DownLeft, ScreenDirections.Bin(naiveMidpoint.X - player.X, naiveMidpoint.Y - player.Y));
        Assert.Equal(StickDirection.Down, ScreenDirections.Bin(south.Position.X - player.X, south.Position.Y - player.Y));
    }

    [Fact]
    public void Exit_bit_with_no_walkable_run_falls_back_to_the_edge_midpoint()
    {
        var input = new TileInput(2, "walled", 0, 0, 32, 32, ExitMaskBits.North, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var walkability = new FakeTileWalkability().Add(2, new WalkGrid(1f, 0f, 0f, 32, 32, new bool[32, 32]));

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));

        Assert.True(opening.UsedFallback);
        Assert.Equal(ExitMaskBits.EdgeMidpoint(input, ExitSide.North), opening.Position);
    }

    [Fact]
    public void Walkable_run_on_a_side_with_no_exit_bit_is_ignored()
    {
        var input = new TileInput(3, "no-exit", 0, 0, 32, 32, ExitMask: 0, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        for (var c = 10; c < 20; c++) cells[0, c] = true;
        var walkability = new FakeTileWalkability().Add(3, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        Assert.Empty(OpeningFinder.Find(map, [tile], members, walkability));
    }

    [Fact]
    public void Two_runs_on_the_same_32_unit_side_de_duplicate_to_one_opening()
    {
        var input = new TileInput(4, "two-runs", 0, 0, 32, 32, ExitMaskBits.North, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        for (var c = 2; c < 6; c++) cells[0, c] = true;   // run 1: cols 2-5
        for (var c = 20; c < 24; c++) cells[0, c] = true; // run 2: cols 20-23
        var walkability = new FakeTileWalkability().Add(4, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));
    }

    [Fact]
    public void A_90_degree_rotated_tile_reads_its_run_from_the_rotated_local_side()
    {
        var input = new TileInput(5, "rotated", 0, 0, 32, 32, ExitMaskBits.East, 1, 1, YawDegrees: 90f);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        for (var c = 10; c < 16; c++) cells[31, c] = true; // local South edge run, cols 10-15
        var walkability = new FakeTileWalkability().Add(5, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));

        Assert.False(opening.UsedFallback);
        Assert.Equal(ExitSide.East, opening.Side); // reported side stays world-facing
        Assert.Equal((-13f, 32f), opening.Position);
        Assert.Equal(6f, opening.Width);
    }

    [Fact]
    public void West_opening_keeps_the_15_wide_real_run_not_the_1_cell_noise()
    {
        // The jail-tile edge profile (WalkGridOrientationTests.JailTileGrid): a 1-cell noise run at
        // row 4 plus the real 15-wide run at rows 9..23, both on the tile's West edge.
        var input = new TileInput(1, "jail", 64, 32, 64, 64, ExitMaskBits.West, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var walkability = new FakeTileWalkability().Add(1, JailTileGrid());

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));

        Assert.False(opening.UsedFallback);
        Assert.Equal(ExitSide.West, opening.Side);
        Assert.Equal(15f, opening.Width);
        Assert.Equal(80.5f, opening.Position.X); // tile OriginX (64) + local X 16.5, the run's centre
    }

    [Fact]
    public void A_1_cell_run_is_rejected_as_noise_even_though_it_meets_the_width_check()
    {
        var input = new TileInput(7, "noise", 0, 0, 32, 32, ExitMaskBits.North, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        cells[0, 16] = true; // 1 unit wide: passes MinOpeningWidthUnits (0.76) but not MinOpeningCells (2)
        var walkability = new FakeTileWalkability().Add(7, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));

        Assert.True(opening.UsedFallback); // no surviving run -> falls back to the edge midpoint
    }

    [Fact]
    public void Two_runs_leading_to_the_same_neighbour_cell_keep_the_widest_not_the_first_found()
    {
        var input = new TileInput(8, "widest-wins", 0, 0, 32, 32, ExitMaskBits.West, 1, 1);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        for (var r = 2; r < 6; r++) cells[r, 0] = true;   // narrower run, found first (rows 2-5, length 4)
        for (var r = 10; r < 26; r++) cells[r, 0] = true; // wider run, found second (rows 10-25, length 16)
        var walkability = new FakeTileWalkability().Add(8, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));

        Assert.Equal(16f, opening.Width); // the wider run, not the narrower one found first
    }

    [Fact]
    public void A_non_axis_yaw_falls_back_instead_of_guessing_a_rotation()
    {
        var input = new TileInput(6, "skewed", 0, 0, 32, 32, ExitMaskBits.North, 1, 1, YawDegrees: 45f);
        var (tile, map, members) = SingleTileRoom(input);
        var cells = new bool[32, 32];
        for (var c = 10; c < 16; c++) cells[0, c] = true;
        var walkability = new FakeTileWalkability().Add(6, new WalkGrid(1f, 0f, 0f, 32, 32, cells));

        var opening = Assert.Single(OpeningFinder.Find(map, [tile], members, walkability));
        Assert.True(opening.UsedFallback);
    }
}
