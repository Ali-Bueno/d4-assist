using System.Numerics;
using D4Data.Format;
using D4MemScan.Scan;

namespace D4MemScan.Tests;

public class ExitConsistencyCheckerTests
{
    private const float TileSize = ExitConsistencyChecker.GridUnit;
    private const int NsewMask = Conventions.ExitNorth | Conventions.ExitSouth | Conventions.ExitEast | Conventions.ExitWest;

    [Fact]
    public void TwoTilesWithMatchingExitsAreConsistent()
    {
        // South neighbour (X = south axis): A needs South, B needs North.
        var tiles = new[]
        {
            new TileFootprint(1, NsewMask, new Vector3(0, 0, 0), TileSize, TileSize),
            new TileFootprint(2, NsewMask, new Vector3(TileSize, 0, 0), TileSize, TileSize),
        };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(1, result.TotalPairs);
        Assert.Equal(1, result.ConsistentPairs);
        Assert.Equal(0, result.SkippedMultiCellTiles);
        Assert.Equal(100.0, result.Percent);
    }

    [Fact]
    public void BorderClosedOnBothSidesIsConsistent()
    {
        var tiles = new[]
        {
            new TileFootprint(1, NsewMask & ~Conventions.ExitSouth, new Vector3(0, 0, 0), TileSize, TileSize),
            new TileFootprint(2, NsewMask & ~Conventions.ExitNorth, new Vector3(TileSize, 0, 0), TileSize, TileSize),
        };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(1, result.TotalPairs);
        Assert.Equal(1, result.ConsistentPairs);
    }

    [Fact]
    public void MissingExitOnOneSideIsInconsistent()
    {
        var noSouthExit = NsewMask & ~Conventions.ExitSouth;
        var tiles = new[]
        {
            new TileFootprint(1, noSouthExit, new Vector3(0, 0, 0), TileSize, TileSize),
            new TileFootprint(2, NsewMask, new Vector3(TileSize, 0, 0), TileSize, TileSize),
        };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(1, result.TotalPairs);
        Assert.Equal(0, result.ConsistentPairs);
    }

    [Fact]
    public void IsolatedTileContributesNoPairs()
    {
        var tiles = new[] { new TileFootprint(1, NsewMask, new Vector3(0, 0, 0), TileSize, TileSize) };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(0, result.TotalPairs);
        Assert.Equal(0, result.ConsistentPairs);
        Assert.Equal(0.0, ExitConsistencyChecker.ConsistencyPercent(result.ConsistentPairs, result.TotalPairs));
    }

    [Fact]
    public void EachAdjacentPairIsCountedOnce()
    {
        // A 2x1 grid of two tiles has exactly one adjacency, whether approached from A or B.
        var tiles = new[]
        {
            new TileFootprint(1, NsewMask, new Vector3(0, 0, 0), TileSize, TileSize),
            new TileFootprint(2, NsewMask, new Vector3(0, TileSize, 0), TileSize, TileSize), // east neighbour (Y = east axis)
        };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(1, result.TotalPairs);
    }

    [Fact]
    public void MultiCellTileIsSkippedRatherThanGuessed()
    {
        // A 64x64 tile can't be placed on the single-cell grid; it must be counted as skipped, not
        // silently dropped or mismapped onto a single cell, and must not crash or corrupt the other counts.
        var tiles = new[]
        {
            new TileFootprint(1, NsewMask, new Vector3(0, 0, 0), TileSize, TileSize),
            new TileFootprint(2, NsewMask, new Vector3(TileSize, 0, 0), TileSize * 2, TileSize * 2),
        };

        var result = ExitConsistencyChecker.CheckAdjacency(tiles);

        Assert.Equal(1, result.SkippedMultiCellTiles);
        Assert.Equal(0, result.TotalPairs); // the 64x64 tile is excluded, leaving tile 1 isolated
    }
}
