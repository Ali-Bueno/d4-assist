using System.Runtime.InteropServices;
using D4Memory.Scan;
using static D4Memory.Safety.NativeMethods;

namespace D4Memory.Safety;

/// <summary>
/// Read-only view of one process's memory. Hard safety policy:
/// - Opens the handle with PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ only.
/// - Never requests SeDebugPrivilege, never writes, never injects, never uses debugging APIs.
/// - Only enumerates MEM_COMMIT regions whose protection is PAGE_READWRITE or PAGE_READONLY
///   (PAGE_GUARD, PAGE_NOACCESS and every executable protection are skipped).
/// - Reads happen in bounded chunks and a failed chunk simply ends that read (no throw, no retry).
/// The handle's lifetime is the caller's `using` scope — one command, then closed.
/// </summary>
public sealed class ProcessReader : IMemoryReader, IDisposable
{
    // Upper bound of user-mode virtual address space on x64 Windows; stops region enumeration from looping forever.
    private const long MaxUserAddress = 0x7FFFFFFEFFFF;

    // Each ReadProcessMemory call is capped so one bad region can only fail a small chunk, not a huge one.
    private const int ReadChunkBytes = 64 * 1024;

    private readonly IntPtr _handle;
    private bool _disposed;

    private ProcessReader(IntPtr handle) => _handle = handle;

    /// <summary>Opens Diablo IV by name (per ProcessLocator) for the duration of the returned reader.</summary>
    public static ProcessReader OpenDiabloIv() => Open(ProcessLocator.Find().Id);

    public static ProcessReader Open(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation | ProcessVmRead, false, (uint)processId);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"OpenProcess({processId}) failed (error {Marshal.GetLastWin32Error()}).");
        return new ProcessReader(handle);
    }

    /// <summary>Heap-scan regions: MEM_COMMIT, PAGE_READWRITE or PAGE_READONLY only (unchanged contract).</summary>
    public IReadOnlyList<MemoryRegion> Regions() => Walk(IsSafeToRead);

    /// <summary>
    /// Image-scoped regions for ModuleLocator: same safety rules plus PAGE_WRITECOPY, since an unmodified
    /// module data page reads as copy-on-write until first write. Heap scans never need this broader filter.
    /// </summary>
    public IReadOnlyList<MemoryRegion> ImageRegions() => Walk(IsSafeImagePage);

    private IReadOnlyList<MemoryRegion> Walk(Func<MemoryBasicInformation, bool> isSafe)
    {
        var regions = new List<MemoryRegion>();
        long address = 0;
        var infoSize = (nuint)Marshal.SizeOf<MemoryBasicInformation>();
        while (address >= 0 && address < MaxUserAddress)
        {
            if (VirtualQueryEx(_handle, (IntPtr)address, out var info, infoSize) == 0) break;
            long regionSize = (long)info.RegionSize;
            if (regionSize <= 0) break; // malformed result; stop rather than spin
            if (isSafe(info))
                regions.Add(new MemoryRegion(address, regionSize, info.Type, (long)info.AllocationBase, info.Protect & BaseProtectionMask));
            address += regionSize;
        }
        return regions;
    }

    public bool TryRead(long address, byte[] buffer, int count)
    {
        int done = 0;
        while (done < count)
        {
            int chunk = Math.Min(ReadChunkBytes, count - done);
            if (!ReadChunk(address + done, buffer, done, chunk)) return false;
            done += chunk;
        }
        return true;
    }

    private unsafe bool ReadChunk(long address, byte[] buffer, int offset, int size)
    {
        fixed (byte* p = buffer)
        {
            bool ok = ReadProcessMemory(_handle, (IntPtr)address, p + offset, (nuint)size, out var read);
            return ok && (long)read == size;
        }
    }

    private static bool IsSafeToRead(MemoryBasicInformation info)
    {
        uint baseProtection = BaseProtection(info);
        return IsCommittedNonGuard(info) && (baseProtection == PageReadOnly || baseProtection == PageReadWrite);
    }

    private static bool IsSafeImagePage(MemoryBasicInformation info)
    {
        uint baseProtection = BaseProtection(info);
        return IsCommittedNonGuard(info)
            && (baseProtection == PageReadOnly || baseProtection == PageReadWrite || baseProtection == PageWriteCopy);
    }

    private static bool IsCommittedNonGuard(MemoryBasicInformation info) =>
        info.State == MemCommit && (info.Protect & PageGuard) == 0;

    private static uint BaseProtection(MemoryBasicInformation info) => info.Protect & BaseProtectionMask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseHandle(_handle);
    }
}
