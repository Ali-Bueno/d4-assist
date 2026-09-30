using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Radar;

namespace D4Assist.Tests;

/// <summary>The radar's world adapter over synthetic tiles: tile A at the origin and tile B just south of it
/// (+X), 32 units each, on different DRLG nodes, so two rooms.</summary>
public class DungeonRadarWorldTests
{
    private const int TileSize = 32;
    private const int SnoA = 101, SnoB = 102, SnoNoData = 103;

    // A: everything walkable except its north edge row. B: a single walkable column at local Y = 16.
    private readonly FakeWalkability _walkability = new FakeWalkability()
        .Add(SnoA, (row, _) => row >= 1)
        .Add(SnoB, (_, col) => col == 16);

    [Fact]
    public void TwoTiles_GiveCellStatesAndSeparateRooms()
    {
        var world = new DungeonRadarWorld(_walkability);

        Assert.True(world.UpdateTiles([Tile(SnoA, 0, 0, node: 1), Tile(SnoB, TileSize, 0, node: 2)]));

        Assert.Equal(RadarCell.Blocked, world.Cell(0, 5));
        Assert.Equal(RadarCell.Open, world.Cell(5, 5));
        Assert.Equal(RadarCell.Open, world.Cell(40, 16));
        Assert.Equal(RadarCell.Blocked, world.Cell(40, 5));
        Assert.Equal(RadarCell.Unknown, world.Cell(-1, 5));
        Assert.Equal(RadarCell.Unknown, world.Cell(70, 5));
        int a = world.RoomAt(5, 5), b = world.RoomAt(40, 16);
        Assert.True(a >= 0 && b >= 0 && a != b);
        Assert.Equal(a, world.RoomAt(0, 5)); // walls belong to their tile's room too
        Assert.Equal(-1, world.RoomAt(70, 5));
    }

    [Fact]
    public void AdjacentTilesOfOneNode_ShareARoom()
    {
        var world = new DungeonRadarWorld(_walkability);

        world.UpdateTiles([Tile(SnoA, 0, 0, node: 1), Tile(SnoB, TileSize, 0, node: 1)]);

        Assert.Equal(world.RoomAt(5, 5), world.RoomAt(40, 16));
    }

    [Fact]
    public void TileWithoutWalkMap_ReadsUnknownButKeepsItsRoom()
    {
        var world = new DungeonRadarWorld(_walkability);

        world.UpdateTiles([Tile(SnoNoData, 0, 0, node: 1)]);

        Assert.Equal(RadarCell.Unknown, world.Cell(5, 5));
        Assert.True(world.RoomAt(5, 5) >= 0);
        Assert.Equal(1, world.TilesWithoutWalkability);
    }

    [Fact]
    public void Visits_AreTrackedPerRoom_AndResetClearsEverything()
    {
        var world = new DungeonRadarWorld(_walkability);
        world.UpdateTiles([Tile(SnoA, 0, 0, node: 1), Tile(SnoB, TileSize, 0, node: 2)]);
        int a = world.RoomAt(5, 5), b = world.RoomAt(40, 16);

        world.MarkVisited(5.5f, 5.5f);

        Assert.True(world.IsVisited(a));
        Assert.False(world.IsVisited(b));

        world.Reset();

        Assert.False(world.IsVisited(a));
        Assert.Equal(RadarCell.Unknown, world.Cell(5, 5));
    }

    [Fact]
    public void RoomIds_SurviveANewTile_AndAnUnchangedSnapshotDoesNotRebuild()
    {
        var world = new DungeonRadarWorld(_walkability);
        var tileA = Tile(SnoA, 0, 0, node: 1);
        world.UpdateTiles([tileA]);
        var a = world.RoomAt(5, 5);

        Assert.False(world.UpdateTiles([tileA]));
        Assert.True(world.UpdateTiles([tileA, Tile(SnoB, -TileSize, 0, node: 2)])); // a lower origin than A

        Assert.Equal(a, world.RoomAt(5, 5));
        Assert.NotEqual(a, world.RoomAt(-16, 16));
    }

    [Fact]
    public void Doorways_NameTheRoomBeyond()
    {
        var world = new DungeonRadarWorld(_walkability);

        world.UpdateTiles([Tile(SnoA, 0, 0, node: 1, exitMask: 2), Tile(SnoB, TileSize, 0, node: 2, exitMask: 1)]);

        int a = world.RoomAt(5, 5), b = world.RoomAt(40, 16);
        var fromA = Assert.Single(world.Doorways, d => d.Room == a);
        Assert.Equal(b, fromA.Beyond);
        Assert.Equal(TileSize, fromA.Centre.X); // on A's south edge
    }

    private static TileState Tile(int sno, float originX, float originY, uint node, int exitMask = 0) =>
        new(sno, $"tile{sno}", originX, originY, TileSize, TileSize, ExitMask: exitMask, NodeId: node, GroupId: node);

    private sealed class FakeWalkability : ITileWalkability
    {
        private readonly Dictionary<int, WalkGrid> _grids = new();

        /// <summary>A 1-unit, tile-sized grid (row = local X, col = local Y, as WalkGrid).</summary>
        public FakeWalkability Add(int sno, Func<int, int, bool> walkable)
        {
            var cells = new bool[TileSize, TileSize];
            for (var r = 0; r < TileSize; r++)
                for (var c = 0; c < TileSize; c++)
                    cells[r, c] = walkable(r, c);
            _grids[sno] = new WalkGrid(1f, 0f, 0f, TileSize, TileSize, cells);
            return this;
        }

        public bool TryGet(int sceneSno, out WalkGrid grid) => _grids.TryGetValue(sceneSno, out grid!);
    }
}
