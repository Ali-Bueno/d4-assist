namespace D4Memory.Scan;

/// <summary>Serves reads from whole pages read once: a walk over nodes that lie together costs one call into the game per page, not
/// per node, and sees them at almost the same moment. Keeps what it read until <see cref="Forget"/>. Not thread-safe.</summary>
public sealed class PagedReads(IMemoryReader inner) : IMemoryReader
{
    private static readonly int PageBytes = Environment.SystemPageSize;

    private readonly Dictionary<long, byte[]?> _pages = [];
    private readonly Stack<byte[]> _spare = new();

    public IReadOnlyList<MemoryRegion> Regions() => inner.Regions();

    public IEnumerable<MemoryRegion> ResidentRuns(MemoryRegion region) => inner.ResidentRuns(region);

    public bool TryRead(long address, byte[] buffer, int count)
    {
        for (int done = 0; done < count;)
        {
            long at = address + done, page = at & ~(PageBytes - 1L);
            int offset = (int)(at - page), chunk = Math.Min(PageBytes - offset, count - done);
            if (PageAt(page) is not { } bytes) return inner.TryRead(address, buffer, count); // a page only partly readable
            Buffer.BlockCopy(bytes, offset, buffer, done, chunk);
            done += chunk;
        }
        return true;
    }

    /// <summary>Drops what was read: the next reads see memory as it is then.</summary>
    public void Forget()
    {
        foreach (var page in _pages.Values)
            if (page is not null) _spare.Push(page);
        _pages.Clear();
    }

    private byte[]? PageAt(long page)
    {
        if (_pages.TryGetValue(page, out var known)) return known;
        var bytes = _spare.TryPop(out var spare) ? spare : new byte[PageBytes];
        if (inner.TryRead(page, bytes, PageBytes)) return _pages[page] = bytes;
        _spare.Push(bytes);
        return _pages[page] = null;
    }
}
