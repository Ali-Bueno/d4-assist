using D4Memory.Scan;

namespace D4Memory.Tests;

/// <summary>In-memory stand-in for ProcessReader: backs "process memory" with plain byte arrays. No process access.</summary>
public sealed class FakeMemoryReader : IMemoryReader
{
    private readonly List<(long Address, byte[] Data)> _regions = [];

    public void AddRegion(long address, byte[] data) => _regions.Add((address, data));

    public IReadOnlyList<MemoryRegion> Regions() => _regions.Select(r => new MemoryRegion(r.Address, r.Data.Length)).ToList();

    public bool TryRead(long address, byte[] buffer, int count)
    {
        foreach (var (regionAddress, data) in _regions)
        {
            if (address < regionAddress || address + count > regionAddress + data.Length) continue;
            Array.Copy(data, address - regionAddress, buffer, 0, count);
            return true;
        }
        return false;
    }
}
