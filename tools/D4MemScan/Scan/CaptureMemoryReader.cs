using D4Memory.Scan;
using System.Text.Json;

namespace D4MemScan.Scan;

/// <summary>Replays a saved `--focus` capture as memory, so matchers can be re-run offline without the game.</summary>
public sealed class CaptureMemoryReader : IMemoryReader
{
    private readonly List<(MemoryRegion Region, byte[] Bytes)> _chunks = [];

    public CaptureMemoryReader(IEnumerable<FocusOccurrence> occurrences)
    {
        foreach (var o in occurrences)
        {
            var bytes = Convert.FromHexString(o.ContextHex);
            _chunks.Add((new MemoryRegion(o.AddressValue + o.ContextStart, bytes.Length), bytes));
        }
    }

    public static CaptureMemoryReader Load(string focusJsonPath) => new(LoadOccurrences(focusJsonPath));

    /// <summary>Raw occurrence list from a capture file, for callers that need more than a memory view
    /// (e.g. PlayerCommand reading a sentinel "module-base" entry out of a `--dump-module` capture).</summary>
    public static IReadOnlyList<FocusOccurrence> LoadOccurrences(string focusJsonPath) =>
        JsonSerializer.Deserialize<List<FocusOccurrence>>(File.ReadAllText(focusJsonPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

    public IReadOnlyList<MemoryRegion> Regions() => [.. _chunks.Select(c => c.Region)];

    public bool TryRead(long address, byte[] buffer, int count)
    {
        foreach (var (region, bytes) in _chunks)
        {
            long offset = address - region.Address;
            if (offset < 0 || offset + count > bytes.Length) continue;
            Array.Copy(bytes, offset, buffer, 0, count);
            return true;
        }
        return false;
    }
}
