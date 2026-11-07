using System.Runtime.InteropServices;

namespace D4Memory.Safety;

/// <summary>Win32 P/Invoke surface used by ProcessReader — read-only memory inspection only.</summary>
/// <remarks>No debug/injection/write APIs are declared anywhere in this project by design.</remarks>
internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out MemoryBasicInformation buffer, nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static unsafe extern bool ReadProcessMemory(
        IntPtr process, IntPtr baseAddress, byte* buffer, nuint size, out nuint bytesRead);

    [DllImport("kernel32.dll", EntryPoint = "K32QueryWorkingSetEx", SetLastError = true)]
    internal static extern bool QueryWorkingSetEx(IntPtr process, [In, Out] WorkingSetPage[] pages, uint size);

    // PSAPI_WORKING_SET_EX_INFORMATION: the page asked about, and its attributes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct WorkingSetPage
    {
        public IntPtr VirtualAddress;
        public nuint Attributes;
    }

    // PSAPI_WORKING_SET_EX_BLOCK.Valid: the page is in the process's working set.
    internal const nuint WorkingSetValid = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    // PROCESS_* access rights (winnt.h) — to read memory and ask which pages are in RAM, nothing else.
    internal const uint ProcessQueryInformation = 0x0400;
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint ProcessVmRead = 0x0010;

    // MEMORY_BASIC_INFORMATION.State (winnt.h).
    internal const uint MemCommit = 0x1000;

    // MEMORY_BASIC_INFORMATION.Type (winnt.h) — a page backed by a mapped PE image (exe/dll).
    internal const uint MemImage = 0x1000000;
    internal const uint MemMapped = 0x40000;
    internal const uint MemPrivate = 0x20000;

    // MEMORY_BASIC_INFORMATION.Protect page-protection constants (winnt.h).
    internal const uint PageNoAccess = 0x01;
    internal const uint PageReadOnly = 0x02;
    internal const uint PageReadWrite = 0x04;
    // Copy-on-write image page (still unmodified since load); only relevant to the image-scoped scan —
    // see ProcessReader.ImageRegions. Heap-scan regions never need it.
    internal const uint PageWriteCopy = 0x08;
    internal const uint PageGuard = 0x100;
    // Memory shared with the GPU: no game object lives there, and reading it costs far more than any other page.
    internal const uint PageNoCache = 0x200;
    internal const uint PageWriteCombine = 0x400;

    // Low byte of Protect holds the base protection; bits above it are modifiers (guard/nocache/writecombine).
    internal const uint BaseProtectionMask = 0xFF;
}
