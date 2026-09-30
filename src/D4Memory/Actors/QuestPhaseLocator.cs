using System.Buffers.Binary;
using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>
/// The tracked quest's current phase (research/client-memory.md §4, quest phases): the module's data holds the pair
/// {u32 quest SNO, u32 current phase UID}, the UID one of the quest's QuestPhase.dwUID values. Live 2026-09-30 (build
/// 3.2.2): it went 236 → 271 when the Darkened Way blood-petal scene ended. Found by content, so no address is needed.
/// </summary>
public static class QuestPhaseLocator
{
    /// <summary>Addresses of the phase word of every {questSno, phase UID} pair in <paramref name="data"/>, in address order.</summary>
    public static IReadOnlyList<long> Find(IMemoryReader memory, IReadOnlyList<MemoryRegion> data, int questSno, IReadOnlySet<uint> phaseUids)
    {
        var found = new SortedSet<long>();
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in data)
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                var span = window.Span;
                for (int i = (int)((4 - address % 4) % 4); i + 8 <= span.Length; i += 4)
                    if (BinaryPrimitives.ReadInt32LittleEndian(span[i..]) == questSno
                        && phaseUids.Contains(BinaryPrimitives.ReadUInt32LittleEndian(span[(i + 4)..])))
                        found.Add(address + i + 4);
            }
        return [.. found];
    }

    /// <summary>The phase UID at <paramref name="phaseAddress"/>; null when the pair no longer holds this quest and one of its phases.</summary>
    public static uint? Read(IMemoryReader memory, long phaseAddress, int questSno, IReadOnlySet<uint> phaseUids)
    {
        if (!PoolContainer.TryReadU32(memory, phaseAddress - sizeof(int), out uint sno) || (int)sno != questSno) return null;
        return PoolContainer.TryReadU32(memory, phaseAddress, out uint phase) && phaseUids.Contains(phase) ? phase : null;
    }
}
