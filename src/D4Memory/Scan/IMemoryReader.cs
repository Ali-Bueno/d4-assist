namespace D4Memory.Scan;

/// <summary>One committed, readable memory region as reported by VirtualQueryEx.</summary>
/// <param name="Type">MEMORY_BASIC_INFORMATION.Type (e.g. MEM_IMAGE); 0 when the caller didn't ask for it.</param>
/// <param name="AllocationBase">MEMORY_BASIC_INFORMATION.AllocationBase; identifies which module/mapping owns the region.</param>
/// <param name="Protection">Base page protection (low byte of MEMORY_BASIC_INFORMATION.Protect, guard/modifier bits stripped).</param>
public readonly record struct MemoryRegion(long Address, long Size, uint Type = 0, long AllocationBase = 0, uint Protection = 0);

/// <summary>Abstracts memory access so the scanner logic is testable without a live process.</summary>
public interface IMemoryReader
{
    /// <summary>Regions safe to read under the scanner's safety policy (see ProcessReader).</summary>
    IReadOnlyList<MemoryRegion> Regions();

    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="address"/> into <paramref name="buffer"/>.</summary>
    /// <remarks>Returns false on any failure (unmapped, protected, partial read); callers skip silently.</remarks>
    bool TryRead(long address, byte[] buffer, int count);
}
