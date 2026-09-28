using D4Memory.Scan;
using System.Collections.Concurrent;
using D4Data.Model;

namespace D4MemScan.Scan;

/// <summary>Builds the initial pose-snapshot candidate list in parallel (see PoseScanner + PoseWalkabilityFilter).</summary>
public static class PoseSnapshotter
{
    public static List<PoseSample> Snapshot(
        IMemoryReader reader, Box bounds, IReadOnlyList<TileHitRecord> tiles, IReadOnlyDictionary<int, SceneInfo> walkability,
        Action<long, long>? onProgress = null)
    {
        var regions = reader.Regions();
        long total = regions.Sum(r => r.Size);
        long scanned = 0;
        var results = new ConcurrentDictionary<long, PoseSample>();

        Parallel.ForEach(regions, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(reader, region, buffer))
                foreach (var sample in PoseScanner.FindCandidates(data.Span, address, bounds))
                    if (PoseWalkabilityFilter.IsWalkableOrUnknown(sample.X, sample.Y, tiles, walkability))
                        results.TryAdd(sample.Address, sample);
            onProgress?.Invoke(Interlocked.Add(ref scanned, region.Size), total);
            return buffer;
        }, _ => { });

        return [.. results.Values.OrderBy(s => s.Address)];
    }
}
