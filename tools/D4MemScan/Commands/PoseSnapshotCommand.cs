using D4Data.Model;
using D4Data.Readers;
using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>`pose-snapshot`: every 4-aligned float triple inside the `scenes` report's world-AABB bounds.</summary>
internal static class PoseSnapshotCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        string reportPath = reader.Require("--scenes");
        string outPath = reader.Get("--out") ?? Path.Combine(Path.GetTempPath(), "d4memscan-pose-snapshot.json");

        var report = JsonOutput.Read<SceneScanReport>(reportPath);
        if (report.Tiles.Count == 0) throw new InvalidDataException($"{reportPath} has no tile hits to bound the search.");

        var (store, schema, _) = GameData.Open(reader);
        using (store)
        {
            var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
            var walkability = LoadWalkability(report.Tiles, sceneReader);

            using var process = ProcessReader.OpenDiabloIv();
            var samples = PoseSnapshotter.Snapshot(process, report.Bounds, report.Tiles, walkability, ProgressPrinter.Create());

            JsonOutput.Write(samples, outPath);
            Console.WriteLine($"Snapshot: {samples.Count} candidates within the dungeon bounds.");
            foreach (var s in samples.Take(10))
                Console.WriteLine($"  0x{s.Address:X} -> ({s.X:F2}, {s.Y:F2}, {s.Z:F2})");
            Console.WriteLine($"Snapshot written to {outPath}");
            return 0;
        }
    }

    /// <summary>Re-reads each tile's walkability offline rather than trusting an embedded copy in the report.</summary>
    private static Dictionary<int, SceneInfo> LoadWalkability(IReadOnlyList<TileHitRecord> tiles, SceneReader sceneReader)
    {
        var result = new Dictionary<int, SceneInfo>();
        foreach (int sno in tiles.Select(t => t.Sno).Distinct())
        {
            try { result[sno] = sceneReader.Read(sno); }
            catch (InvalidDataException) { } // malformed/placeholder scene definition; skip, keep going
        }
        return result;
    }
}
