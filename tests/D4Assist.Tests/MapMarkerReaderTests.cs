using System.Buffers.Binary;
using D4Assist.Game;
using D4Memory.Actors;
using D4Memory.Scan;

namespace D4Assist.Tests;

public class MapMarkerReaderTests
{
    // research/dungeon-objectives.md §19: the head sits in the game object's heap reservation, [H-8] the 0xA0-byte node pool.
    private const long ModuleBase = 0x7FF7_0000_0000, GameObjectRva = 0x100, HeapBase = 0x1000_0000;
    private const long GameObject = HeapBase + 0x100, Pool = HeapBase + 0x200, Head = HeapBase + 0x408, First = HeapBase + 0x1000, Second = HeapBase + 0x1100;
    private const int DoorSno = 2640816, LeverSno = 2640808, LairArea = 2641357, Generation = 19;

    private sealed class FakeMemory : IMemoryReader
    {
        private readonly byte[] _module = new byte[0x1000], _heap = new byte[0x10000];

        public FakeMemory()
        {
            U64(_module, GameObjectRva, GameObject);
            U32(_heap, Pool - HeapBase + 0x20, 0xA0);
            U64(_heap, Head - 8 - HeapBase, Pool);
            U64(_heap, Head - HeapBase, First);
            U64(_heap, Head + 8 - HeapBase, Second);
            U64(_heap, Head + 0x10 - HeapBase, 2);
            Node(First, next: Second, prev: Head, DoorSno, -1115.05f, -219.36f);
            Node(Second, next: Head, prev: First, LeverSno, -1068.8f, -144f);
        }

        public IReadOnlyList<MemoryRegion> Regions() =>
            [new(ModuleBase, _module.Length, AllocationBase: ModuleBase), new(HeapBase, _heap.Length, AllocationBase: HeapBase)];

        public bool TryRead(long address, byte[] buffer, int count)
        {
            var (memory, start) = address >= ModuleBase ? (_module, address - ModuleBase) : (_heap, address - HeapBase);
            if (start < 0 || start + count > memory.Length) return false;
            Array.Copy(memory, start, buffer, 0, count);
            return true;
        }

        public void SetGeneration(long node, uint value) => U32(_heap, node - HeapBase + 0x10, value);

        public void BreakRing() => U64(_heap, Second - HeapBase, Second);

        public void MendRing() => U64(_heap, Second - HeapBase, Head);

        public void ClearPool() => U32(_heap, Pool - HeapBase + 0x20, 0);

        private void Node(long at, long next, long prev, int sno, float x, float y)
        {
            long o = at - HeapBase;
            U64(_heap, o, next);
            U64(_heap, o + 8, prev);
            U32(_heap, o + 0x10, Generation);
            U32(_heap, o + 0x18, Generation);
            U32(_heap, o + 0x1C, (uint)sno);
            BinaryPrimitives.WriteSingleLittleEndian(_heap.AsSpan((int)o + 0x20), x);
            BinaryPrimitives.WriteSingleLittleEndian(_heap.AsSpan((int)o + 0x24), y);
            U32(_heap, o + 0x38, LairArea);
            U32(_heap, o + 0x50, MapMarkerReader.KindGizmo);
        }

        private static void U64(byte[] memory, long at, long value) => BinaryPrimitives.WriteInt64LittleEndian(memory.AsSpan((int)at), value);
        private static void U32(byte[] memory, long at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan((int)at), value);
    }

    [Fact]
    public void Finds_the_head_in_the_game_objects_reservation_and_reads_every_marker()
    {
        var memory = new FakeMemory();

        Assert.Equal(Head, MapMarkerReader.Locate(memory, ModuleBase, GameObjectRva));
        var markers = MapMarkerReader.TryRead(memory, Head)!;
        Assert.Equal([DoorSno, LeverSno], markers.Select(m => m.ActorSno));
        Assert.Equal(-1115.05f, markers[0].Position.X, 0.01f);
        Assert.Equal(LairArea, markers[0].LevelAreaSno);
    }

    [Fact]
    public void A_node_of_another_generation_breaks_the_list()
    {
        var memory = new FakeMemory();
        memory.SetGeneration(Second, Generation + 2); // mid-rebuild, or another list

        Assert.Null(MapMarkerReader.TryRead(memory, Head));
    }

    [Fact]
    public void A_head_found_earlier_reads_nodes_of_two_generations()
    {
        var memory = new FakeMemory();
        memory.SetGeneration(Second, Generation + 2);

        Assert.Equal([DoorSno, LeverSno], MapMarkerReader.TryReadFound(memory, Head)!.Select(m => m.ActorSno));
    }

    [Fact]
    public void The_list_is_searched_once_however_many_reads_fail()
    {
        var memory = new FakeMemory();
        int scans = 0;
        var tracker = TrackerOver(memory, () => scans++);
        ReadUntilFound(tracker, memory);

        memory.BreakRing();
        for (int read = 0; read < MapMarkerTracker.RescanAfterFailures * 3; read++) Assert.Null(Read(tracker, memory));
        memory.MendRing();

        Assert.NotNull(Read(tracker, memory));
        Assert.Equal(1, scans);
    }

    [Fact]
    public void Failed_reads_are_logged_once_a_run_is_long_and_when_it_ends()
    {
        var memory = new FakeMemory();
        var logged = new List<string>();
        var tracker = TrackerOver(memory, () => { }, logged.Add);
        ReadUntilFound(tracker, memory);
        logged.Clear();

        memory.BreakRing();
        for (int read = 0; read < MapMarkerTracker.RescanAfterFailures * 2; read++) Read(tracker, memory);
        memory.MendRing();
        Read(tracker, memory);

        Assert.Equal(
            [$"MARKERS list not read {MapMarkerTracker.RescanAfterFailures} times in a row, its head kept",
             $"MARKERS list read again after {MapMarkerTracker.RescanAfterFailures * 2} failed reads"],
            logged);
    }

    [Fact]
    public void A_head_with_no_marker_nodes_behind_it_is_searched_again()
    {
        var memory = new FakeMemory();
        int scans = 0;
        var tracker = TrackerOver(memory, () => Interlocked.Increment(ref scans));
        ReadUntilFound(tracker, memory);

        memory.ClearPool();
        for (int read = 0; read < MapMarkerTracker.RescanAfterFailures; read++) Assert.Null(Read(tracker, memory));
        Assert.True(SpinWait.SpinUntil(() => { Read(tracker, memory); return Volatile.Read(ref scans) == 2; }, TimeSpan.FromSeconds(5)));
    }

    // A clock far ahead each read, so a retry is always due.
    private static MapMarkerTracker TrackerOver(FakeMemory memory, Action onScan, Action<string>? log = null)
    {
        long ticks = 0;
        return new MapMarkerTracker(() => { onScan(); return memory; }, () => TimeSpan.FromMinutes(Interlocked.Increment(ref ticks)), log);
    }

    private static IReadOnlyList<MapMarker>? Read(MapMarkerTracker tracker, FakeMemory memory) => tracker.Read(memory, ModuleBase, GameObjectRva);

    private static void ReadUntilFound(MapMarkerTracker tracker, FakeMemory memory) =>
        Assert.True(SpinWait.SpinUntil(() => Read(tracker, memory) is not null, TimeSpan.FromSeconds(5)));
}
