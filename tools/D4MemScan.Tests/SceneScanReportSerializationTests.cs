using System.Text.Json;
using D4Data.Model;
using D4MemScan.Scan;

namespace D4MemScan.Tests;

/// <summary>
/// pose-snapshot reads a `scenes` report back from JSON (see Commands/JsonOutput.cs); this proves the report's
/// record types round-trip through System.Text.Json the same way the CLI serializes them.
/// </summary>
public class SceneScanReportSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void TilesBoundsAndRevealedListRoundTrip()
    {
        var tile = new TileHitRecord("0x1000", 1234, "ProtoDun_Keep_N_01", [64f, 96f, 0f], [0f, 0f, 0f, 1f],
            15, 0x20003, true, "0xDEADBEEF");
        var report = new SceneScanReport("D4MemScan", new SnoRef(843152, "DGN_Hawe_HeathensKeep"),
            [tile], [new VtableCount("0xDEADBEEF", 1)],
            new Box([64f, 96f, -20f], [96f, 128f, 20f]),
            new ExitConsistencyResult(1, 1, 100.0, 0),
            [new RevealedRecord(0x80005, 1234, "ProtoDun_Keep_N_01", 7)],
            ["NSEW"], ["note"]);

        var json = JsonSerializer.Serialize(report, Options);
        var roundTripped = JsonSerializer.Deserialize<SceneScanReport>(json, Options)!;

        Assert.Equal(report.World!.Sno, roundTripped.World!.Sno);
        Assert.Equal(64f, roundTripped.Bounds.Min[0]);
        Assert.Equal(1234, roundTripped.Tiles[0].Sno);
        Assert.True(roundTripped.Tiles[0].HashMatchesFormula);
        Assert.Equal(0, roundTripped.ExitConsistency.SkippedMultiCellTiles);
        Assert.Equal(0x80005u, roundTripped.Revealed[0].IdSScene);
        Assert.Equal("NSEW", roundTripped.AsciiMap[0]);
    }
}
