using System.Numerics;
using D4Data.Format;
using D4Data.Model;
using D4Memory.Scan;
using D4MemScan.Scan;

namespace D4MemScan.Tests;

public class AsciiMapBuilderTests
{
    private const float Unit = ExitConsistencyChecker.GridUnit;

    [Fact]
    public void RendersSingleAndMultiCellTilesByTheirExitLabel()
    {
        var candidates = new Dictionary<int, CandidateScene>
        {
            [1] = new(1, "A", [32f, 32f], 0, new Box([0, 0, 0], [0, 0, 0])),
            [2] = new(2, "B", [32f, 32f], 0, new Box([0, 0, 0], [0, 0, 0])),
            [3] = new(3, "C", [64f, 64f], 0, new Box([0, 0, 0], [0, 0, 0])),
        };
        var nsew = Conventions.ExitNorth | Conventions.ExitSouth | Conventions.ExitEast | Conventions.ExitWest;
        var tiles = new[]
        {
            Hit(1, nsew, new Vector3(0, 0, 0)),
            Hit(2, Conventions.ExitSouth, new Vector3(0, Unit, 0)),
            Hit(3, 0, new Vector3(Unit, 0, 0)), // 64x64: fills cells (1,0),(1,1),(2,0),(2,1)
        };

        var map = AsciiMapBuilder.Build(tiles, candidates);

        Assert.Equal(new[] { "NSEW S", "- -", "- -" }, map);
    }

    [Fact]
    public void EmptyTileListProducesAnEmptyMap() =>
        Assert.Empty(AsciiMapBuilder.Build([], new Dictionary<int, CandidateScene>()));

    private static CSceneHit Hit(int sno, int exitMask, Vector3 wp) =>
        new(0, sno, null, wp, Quaternion.Identity, exitMask, 0, false, 0, Vector3.Zero, Vector3.Zero, 0, 0);
}
