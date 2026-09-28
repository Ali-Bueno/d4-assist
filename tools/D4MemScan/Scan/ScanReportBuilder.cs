using D4Memory.Scan;
using System.Numerics;
using D4Data.Model;

namespace D4MemScan.Scan;

/// <summary>Turns raw matcher hits into the self-validated report (see `scenes` command).</summary>
public static class ScanReportBuilder
{
    /// <summary>Vertical slack around the tiles' world-AABB Z range for the pose-snapshot bounding box.</summary>
    public const float PoseZMargin = 20f;

    public static SceneScanReport Build(SceneScanResult scan, IReadOnlyDictionary<int, CandidateScene> candidates, SnoRef? world)
    {
        var notes = new List<string>
        {
            "CScene hits are validated against the game's own offline AABB, exit mask and hash-copy fields " +
            "(see CLAUDE.md task spec / research/client-memory.md §1) — a match is a structural signal, not a value coincidence.",
            "Exit consistency skips tiles that span more than one grid cell; see ExitConsistencyChecker.",
        };

        var distinctTiles = scan.Tiles.GroupBy(h => (h.Sno, h.Wp)).Select(g => g.First()).ToList();

        var tiles = ToTileRecords(distinctTiles);
        var vtables = distinctTiles.GroupBy(h => h.Vtable)
            .Select(g => new VtableCount($"0x{g.Key:X}", g.Count()))
            .OrderByDescending(v => v.Count).ToList();

        var footprints = distinctTiles.Select(h => new TileFootprint(h.Sno, h.ExitMask, h.Wp, FootprintX(h, candidates), FootprintY(h, candidates))).ToList();
        var exitConsistency = ExitConsistencyChecker.CheckAdjacency(footprints);

        var revealed = scan.Revealed
            .Select(r => new RevealedRecord(r.IdSScene, r.Sno, r.Name, r.K))
            .Distinct().OrderBy(r => r.Sno).ToList();

        var asciiMap = AsciiMapBuilder.Build(distinctTiles, candidates);
        var bounds = ComputeWorldBoundsUnion(distinctTiles);

        return new SceneScanReport("D4MemScan", world, tiles, vtables, bounds, exitConsistency, revealed, asciiMap, notes);
    }

    private static List<TileHitRecord> ToTileRecords(IReadOnlyList<CSceneHit> hits) =>
        [.. hits.OrderBy(h => h.Address).Select(h => new TileHitRecord(
            $"0x{h.Address:X}", h.Sno, h.Name, [h.Wp.X, h.Wp.Y, h.Wp.Z], [h.Q.X, h.Q.Y, h.Q.Z, h.Q.W],
            h.ExitMask, h.TileHash, h.HashMatchesFormula, $"0x{h.Vtable:X}"))];

    private static float FootprintX(CSceneHit hit, IReadOnlyDictionary<int, CandidateScene> candidates) =>
        candidates.TryGetValue(hit.Sno, out var c) && c.TileSize.Length > 0 && c.TileSize[0] > 0 ? c.TileSize[0] : ExitConsistencyChecker.GridUnit;

    private static float FootprintY(CSceneHit hit, IReadOnlyDictionary<int, CandidateScene> candidates) =>
        candidates.TryGetValue(hit.Sno, out var c) && c.TileSize.Length > 1 && c.TileSize[1] > 0 ? c.TileSize[1] : ExitConsistencyChecker.GridUnit;

    /// <summary>Union of every tile's already-validated world AABB (center/half read straight off the record), not
    /// recomputed from the local AABB + wp, so a rotated tile's swapped axes are still bounded correctly.</summary>
    private static Box ComputeWorldBoundsUnion(IReadOnlyList<CSceneHit> tiles)
    {
        if (tiles.Count == 0) return new Box([0, 0, 0], [0, 0, 0]);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var t in tiles)
        {
            min = Vector3.Min(min, t.WorldAabbCenter - t.WorldAabbHalf);
            max = Vector3.Max(max, t.WorldAabbCenter + t.WorldAabbHalf);
        }
        return new Box([min.X, min.Y, min.Z - PoseZMargin], [max.X, max.Y, max.Z + PoseZMargin]);
    }
}
