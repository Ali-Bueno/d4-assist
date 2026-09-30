namespace D4Memory.Scan;

/// <summary>Shared region-walking used by both the scene scanner and the pose scanner.</summary>
public static class RegionWindows
{
    // Read window: small enough that one failed chunk only loses a small slice of a region, not all of it.
    public const int DefaultWindowBytes = 1024 * 1024;

    // Overlap between consecutive windows so a record straddling a window boundary is still seen whole.
    public const int DefaultOverlapBytes = 4096;

    /// <summary>Yields each readable window's bytes with its base address. The buffer is reused; consume it before moving on.</summary>
    public static IEnumerable<(long Address, ReadOnlyMemory<byte> Data)> Read(
        IMemoryReader reader, int windowBytes = DefaultWindowBytes, int overlapBytes = DefaultOverlapBytes)
    {
        var buffer = new byte[windowBytes];
        foreach (var region in reader.Regions())
            foreach (var window in ReadRegion(reader, region, buffer, overlapBytes))
                yield return window;
    }

    /// <summary>Yields one region's windows into the caller's buffer (one buffer per thread when scanning in parallel).</summary>
    public static IEnumerable<(long Address, ReadOnlyMemory<byte> Data)> ReadRegion(
        IMemoryReader reader, MemoryRegion region, byte[] buffer, int overlapBytes = DefaultOverlapBytes)
    {
        long offset = 0;
        while (offset < region.Size)
        {
            int size = (int)Math.Min(buffer.Length, region.Size - offset);
            if (reader.TryRead(region.Address + offset, buffer, size))
                yield return (region.Address + offset, new ReadOnlyMemory<byte>(buffer, 0, size));
            if (offset + size >= region.Size) break; // this window reached the region's end
            offset += Math.Max(1, size - overlapBytes);
        }
    }
}
