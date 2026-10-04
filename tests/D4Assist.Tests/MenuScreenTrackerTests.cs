using System.Buffers.Binary;
using D4Assist.Game;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4Assist.Tests;

public class MenuScreenTrackerTests
{
    // Windows constants: MEM_PRIVATE and PAGE_READWRITE (MEMORY_BASIC_INFORMATION).
    private const uint Private = 0x20000, ReadWrite = 0x04;
    private const long TableAddress = 0x1000_0000, RecordAddress = 0x1000_0800, ModulePointer = 0x7FF70064D210;
    private const int TableSize = 0x1000, FlagOffset = 0x10, ModulePointerOffset = 0x58, EntryOffset = 0x100;
    private static readonly ModuleRange Module = new(0x7FF700000000, 0x4000000);

    [Fact]
    public void ScansOnceInTheBackground_ThenReadsTheFlagEachPoll()
    {
        var memory = new HeapReader();
        var clock = new Clock();
        var scans = 0;
        var tracker = new MenuScreenTracker(() => { scans++; return memory; }, clock.Now);

        Assert.Null(tracker.Read(memory, Module)); // not located yet
        WaitFor(() => tracker.Read(memory, Module) is not null);
        Assert.Empty(tracker.Read(memory, Module)!);

        memory.SetOpen(true);
        Assert.Equal([MenuScreens.MainMenu], tracker.Read(memory, Module));
        Assert.Equal(1, scans);
    }

    [Fact]
    public void A_world_change_locates_the_records_again_even_when_they_still_validate()
    {
        // Live 2026-10-04: after a character switch an old record read open for minutes and muted every cue.
        var memory = new HeapReader();
        var clock = new Clock();
        var scans = 0;
        var tracker = new MenuScreenTracker(() => { scans++; return memory; }, clock.Now);
        tracker.Read(memory, Module);
        WaitFor(() => tracker.Read(memory, Module) is not null);

        tracker.Relocate();
        Assert.Null(tracker.Read(memory, Module)); // records dropped: nothing mutes until they are found again
        WaitFor(() => scans == 2 && tracker.Read(memory, Module) is not null);
    }

    [Fact]
    public void A_screen_open_too_long_is_checked_against_a_fresh_scan()
    {
        // Live 2026-10-04: a stale record read open for minutes and muted every cue.
        var memory = new HeapReader();
        var clock = new Clock();
        var scans = 0;
        var tracker = new MenuScreenTracker(() => { scans++; return memory; }, clock.Now);
        tracker.Read(memory, Module);
        WaitFor(() => tracker.Read(memory, Module) is not null);
        memory.SetOpen(true);
        memory.MoveRecord(RecordAddress + 0x100);

        Assert.Equal([MenuScreens.MainMenu], tracker.Read(memory, Module)); // the stale record
        clock.Advance(MenuScreenTracker.VerifyOpenAfter);
        tracker.Read(memory, Module);
        WaitFor(() => scans == 2 && tracker.Read(memory, Module) is { Count: 0 });
    }

    [Fact]
    public void RescansOnlyAfterTheRetryDelayOnceARecordStopsValidating()
    {
        var memory = new HeapReader();
        var clock = new Clock();
        var scans = 0;
        var tracker = new MenuScreenTracker(() => { scans++; return memory; }, clock.Now);
        tracker.Read(memory, Module);
        WaitFor(() => tracker.Read(memory, Module) is not null);

        memory.Corrupt();
        Assert.Null(tracker.Read(memory, Module)); // invalid, but the rescan is inside the retry delay
        Assert.Equal(1, scans);

        clock.Advance(MenuScreenTracker.FirstRetryDelay);
        tracker.Read(memory, Module);
        WaitFor(() => scans == 2);
    }

    [Fact]
    public void EachMissDoublesTheWaitBeforeTheNextScan()
    {
        var memory = new HeapReader();
        memory.Corrupt();
        var clock = new Clock();
        int scans = 0, finished = 0;
        var tracker = new MenuScreenTracker(() => { scans++; return memory; }, clock.Now, _ => Interlocked.Increment(ref finished));
        tracker.Read(memory, Module);
        WaitFor(() => finished == 1); // miss: the next scan waits FirstRetryDelay

        clock.Advance(MenuScreenTracker.FirstRetryDelay);
        tracker.Read(memory, Module);
        WaitFor(() => finished == 2); // second miss: the wait doubles

        clock.Advance(MenuScreenTracker.FirstRetryDelay);
        tracker.Read(memory, Module);
        Assert.Equal(2, scans);

        clock.Advance(MenuScreenTracker.FirstRetryDelay);
        tracker.Read(memory, Module);
        WaitFor(() => scans == 3);
    }

    private static void WaitFor(Func<bool> condition)
    {
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
    }

    private sealed class Clock
    {
        private TimeSpan _now = TimeSpan.FromMinutes(1);

        public TimeSpan Now() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class HeapReader : IMemoryReader
    {
        private readonly byte[] _table = new byte[TableSize];

        public HeapReader()
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_table.AsSpan(EntryOffset + 8), ScreenRegistryReader.Hash(MenuScreens.MainMenu));
            BinaryPrimitives.WriteInt64LittleEndian(_table.AsSpan(EntryOffset + 16), RecordAddress);
            BinaryPrimitives.WriteInt64LittleEndian(_table.AsSpan((int)(RecordAddress - TableAddress) + ModulePointerOffset), ModulePointer);
        }

        public void SetOpen(bool open) => BinaryPrimitives.WriteUInt32LittleEndian(_table.AsSpan((int)(RecordAddress - TableAddress) + FlagOffset), open ? 1u : 0u);

        // The UI rebuilt: the registry now points to a new record (closed); the old one still validates and reads open.
        public void MoveRecord(long to)
        {
            BinaryPrimitives.WriteInt64LittleEndian(_table.AsSpan(EntryOffset + 16), to);
            BinaryPrimitives.WriteInt64LittleEndian(_table.AsSpan((int)(to - TableAddress) + ModulePointerOffset), ModulePointer);
        }

        public void Corrupt() => BinaryPrimitives.WriteInt64LittleEndian(_table.AsSpan((int)(RecordAddress - TableAddress) + ModulePointerOffset), 0);

        public IReadOnlyList<MemoryRegion> Regions() => [new MemoryRegion(TableAddress, TableSize, Private, Protection: ReadWrite)];

        public bool TryRead(long address, byte[] buffer, int count)
        {
            if (address < TableAddress || address + count > TableAddress + TableSize) return false;
            Array.Copy(_table, address - TableAddress, buffer, 0, count);
            return true;
        }
    }
}
