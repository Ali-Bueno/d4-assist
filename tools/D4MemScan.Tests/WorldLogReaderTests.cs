using D4MemScan.Scan;

namespace D4MemScan.Tests;

public class WorldLogReaderTests
{
    [Fact]
    public void ExtractsWorldNameFromMarkerLine()
    {
        var lines = new[] { "[Game] Client entered world | world: DGN_Hawe_HeathensKeep | CWorldID: 4194306" };

        Assert.Equal("DGN_Hawe_HeathensKeep", WorldLogReader.LastWorldName(lines));
    }

    [Fact]
    public void KeepsASpaceInTheWorldName()
    {
        var lines = new[] { "[Game] Client entered world | world: CSD_Skov_Philios_ River_Skovos_MorluDungeon | CWorldID: 524291" };

        Assert.Equal("CSD_Skov_Philios_ River_Skovos_MorluDungeon", WorldLogReader.LastWorldName(lines));
    }

    [Fact]
    public void UsesTheLastMatchingLineWhenSeveralWorldsWereEntered()
    {
        var lines = new[]
        {
            "[Game] Client entered world | world: PIT_Step_City",
            "some unrelated log line",
            "[Game] Client entered world | world: DGN_Hawe_HeathensKeep",
        };

        Assert.Equal("DGN_Hawe_HeathensKeep", WorldLogReader.LastWorldName(lines));
    }

    [Fact]
    public void ReturnsNullWhenNoLineMatches()
    {
        Assert.Null(WorldLogReader.LastWorldName(["unrelated line"]));
    }
}
