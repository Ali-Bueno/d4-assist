using System.Buffers.Binary;
using System.Collections.Concurrent;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4Memory.Tests;

public class ScreenRegistryReaderTests
{
    // Windows constants: MEM_PRIVATE and PAGE_READWRITE (MEMORY_BASIC_INFORMATION).
    private const uint Private = 0x20000, ReadWrite = 0x04, Mapped = 0x40000;
    private static readonly ModuleRange Module = new(0x7FF700000000, 0x4000000);
    private const long ModulePointer = 0x7FF70064D210;
    private const long TableRegion = 0x1000_0000, RecordRegion = 0x2000_0000, TextRegion = 0x3000_0000, LaterRegion = 0x4000_0000;
    // The entry's padding after the u32 key holds leftover heap bytes (0x258 live, 2026-09-29).
    private const uint LeftoverPadding = 0x258;
    private const int FlagOffset = 0x10, ModulePointerOffset = 0x58;

    [Fact]
    public void Hash_MatchesLiveValues()
    {
        Assert.Equal(0xE4407FA8u, ScreenRegistryReader.Hash("screen_MenuPanel_Default"));
        Assert.Equal(0x76082EEAu, ScreenRegistryReader.Hash("screen_MainInGameMenu"));
    }

    [Fact]
    public void Locate_FindsTheRecordAndRejectsTheStringDecoy()
    {
        var memory = new FakeMemoryReader();
        var table = new byte[0x1000];
        Entry(table, 0x100, MenuScreens.CharacterPanel, TextRegion); // decoy first: its value +0x58 is text
        Entry(table, 0x200, MenuScreens.CharacterPanel, RecordRegion);
        memory.AddRegion(TableRegion, table, Private, ReadWrite);
        memory.AddRegion(TextRegion, Text(), Private, ReadWrite);
        memory.AddRegion(RecordRegion, Record(open: true), Private, ReadWrite);

        var found = ScreenRegistryReader.Locate(memory, [MenuScreens.CharacterPanel], Module);

        Assert.Equal(RecordRegion, found[MenuScreens.CharacterPanel]);
    }

    [Fact]
    public void Locate_IgnoresNonPrivateRegions()
    {
        var memory = new FakeMemoryReader();
        var table = new byte[0x1000];
        Entry(table, 0x100, MenuScreens.MainMenu, RecordRegion);
        memory.AddRegion(TableRegion, table, Mapped, ReadWrite);
        memory.AddRegion(RecordRegion, Record(open: false), Private, ReadWrite);

        Assert.Empty(ScreenRegistryReader.Locate(memory, [MenuScreens.MainMenu], Module));
    }

    [Fact]
    public void Locate_StopsReadingTheRegionOnceEveryNameIsFound()
    {
        // Regions are scanned in parallel, so only the finding thread's own region is sure to stop early.
        var inner = new FakeMemoryReader();
        var table = new byte[2 * RegionWindows.DefaultWindowBytes];
        Entry(table, 0x100, MenuScreens.CharacterPanel, RecordRegion);
        inner.AddRegion(TableRegion, table, Private, ReadWrite);
        inner.AddRegion(RecordRegion, Record(open: false), Private, ReadWrite);
        var memory = new SpyReader(inner);

        Assert.Single(ScreenRegistryReader.Locate(memory, [MenuScreens.CharacterPanel], Module));

        long secondWindow = TableRegion + RegionWindows.DefaultWindowBytes - RegionWindows.DefaultOverlapBytes;
        Assert.DoesNotContain(secondWindow, memory.ReadAddresses);
    }

    [Fact]
    public void TryReadOpen_ReportsTheFlagAndRejectsAForeignRecord()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(RecordRegion, Record(open: true), Private, ReadWrite);
        memory.AddRegion(TextRegion, Text(), Private, ReadWrite);
        memory.AddRegion(LaterRegion, Record(open: false), Private, ReadWrite);

        Assert.True(ScreenRegistryReader.TryReadOpen(memory, RecordRegion, Module));
        Assert.False(ScreenRegistryReader.TryReadOpen(memory, LaterRegion, Module));
        Assert.Null(ScreenRegistryReader.TryReadOpen(memory, TextRegion, Module));
        Assert.Null(ScreenRegistryReader.TryReadOpen(memory, 0x5000_0000, Module));
    }

    private static void Entry(byte[] table, int offset, string name, long value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(offset + 8), ScreenRegistryReader.Hash(name));
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(offset + 12), LeftoverPadding);
        BinaryPrimitives.WriteInt64LittleEndian(table.AsSpan(offset + 16), value);
    }

    private static byte[] Record(bool open)
    {
        var data = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(FlagOffset), open ? 1u : 0u);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(ModulePointerOffset), ModulePointer);
        return data;
    }

    private static byte[] Text()
    {
        var data = new byte[0x100];
        System.Text.Encoding.ASCII.GetBytes("screen_MenuPanel_Default").CopyTo(data, ModulePointerOffset);
        return data;
    }

    private sealed class SpyReader(IMemoryReader inner) : IMemoryReader
    {
        public ConcurrentQueue<long> ReadAddresses { get; } = new();

        public IReadOnlyList<MemoryRegion> Regions() => inner.Regions();

        public bool TryRead(long address, byte[] buffer, int count)
        {
            ReadAddresses.Enqueue(address);
            return inner.TryRead(address, buffer, count);
        }
    }
}
