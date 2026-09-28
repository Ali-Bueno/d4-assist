using System.Collections.Concurrent;

namespace D4Memory.Scan;

/// <summary>Raw scan output: every distinct CScene tile hit and every revealed-scene list entry found.</summary>
public sealed record SceneScanResult(IReadOnlyList<CSceneHit> Tiles, IReadOnlyList<RevealedHit> Revealed);

/// <summary>Walks every safe memory region in parallel, running both record matchers over each window.</summary>
public sealed class SceneScanner
{
    /// <param name="onProgress">Called after each region with (bytes scanned, total bytes); may run on any thread.</param>
    public SceneScanResult Scan(
        IMemoryReader reader, CandidateIndex candidates, Action<long, long>? onProgress = null, FocusCollector? focus = null)
    {
        var regions = reader.Regions();
        long total = regions.Sum(r => r.Size);
        long scanned = 0;
        var tiles = new ConcurrentDictionary<CSceneHit, byte>();
        var revealed = new ConcurrentDictionary<RevealedHit, byte>();

        Parallel.ForEach(regions, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(reader, region, buffer))
            {
                foreach (var hit in CSceneRecordMatcher.FindHits(data.Span, address, candidates))
                    tiles.TryAdd(hit, 0);
                foreach (var hit in RevealedListMatcher.FindHits(data.Span, address, candidates))
                    revealed.TryAdd(hit, 0);
                focus?.Collect(data.Span, address);
            }
            onProgress?.Invoke(Interlocked.Add(ref scanned, region.Size), total);
            return buffer;
        }, _ => { });

        return new SceneScanResult([.. tiles.Keys], [.. revealed.Keys]);
    }
}
