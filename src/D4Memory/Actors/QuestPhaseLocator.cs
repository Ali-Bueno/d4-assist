using System.Buffers.Binary;
using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>One {quest SNO, phase UID} pair found in the module data: the address of its phase word and the phase it held.</summary>
public readonly record struct PhaseCandidate(long Address, uint Phase);

/// <summary>
/// The tracked quest's current phase (research/client-memory.md §4, quest phases): the module's data holds the pair
/// {u32 quest SNO, u32 current phase UID}, the UID one of the quest's QuestPhase.dwUID values. Live 2026-09-30 (build
/// 3.2.2): it went 236 → 271 when the Darkened Way blood-petal scene ended. Found by content, so no address is needed.
/// </summary>
/// <remarks>
/// A quest whose phases include UID 0 (60 of 162 DPOs, often a middle step: the Lost Archives' carry phase) matches any spot
/// where its SNO is followed by a zero word, and the lowest such spot used to win: the Lost Archives read "phase 0" from the
/// entrance on (live 2026-10-16), so its later phases never named their objects. A pair holding another UID is the stronger
/// evidence and is taken first; a phase read there still follows the quest into phase 0.
/// </remarks>
public static class QuestPhaseLocator
{
    /// <summary>Addresses of the phase word of every {questSno, phase UID} pair in <paramref name="data"/>: the pairs holding a
    /// phase other than 0 first, each group in address order.</summary>
    public static IReadOnlyList<long> Find(IMemoryReader memory, IReadOnlyList<MemoryRegion> data, int questSno, IReadOnlySet<uint> phaseUids) =>
        [.. Ordered(FindCandidates(memory, data, questSno, phaseUids)).Select(c => c.Address)];

    /// <summary>Every {questSno, phase UID} pair in <paramref name="data"/>, in address order.</summary>
    public static IReadOnlyList<PhaseCandidate> FindCandidates(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> data, int questSno, IReadOnlySet<uint> phaseUids) =>
        FindAllCandidates(memory, data, new Dictionary<int, IReadOnlySet<uint>> { [questSno] = phaseUids }).GetValueOrDefault(questSno) ?? [];

    /// <summary>For each of <paramref name="quests"/> (quest SNO → its phase UIDs) with a pair in <paramref name="data"/>, the
    /// address of its phase word (<see cref="Pick"/>): one pass for every quest in progress (a quest no longer in progress has no pair).</summary>
    public static IReadOnlyDictionary<int, long> FindAll(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> data, IReadOnlyDictionary<int, IReadOnlySet<uint>> quests) =>
        FindAllCandidates(memory, data, quests).ToDictionary(kv => kv.Key, kv => Pick(kv.Value));

    /// <summary>Every pair of each of <paramref name="quests"/>, in address order; quests without one are left out.</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<PhaseCandidate>> FindAllCandidates(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> data, IReadOnlyDictionary<int, IReadOnlySet<uint>> quests)
    {
        var found = new Dictionary<int, List<PhaseCandidate>>();
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in data)
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                var span = window.Span;
                for (int i = (int)((4 - address % 4) % 4); i + 8 <= span.Length; i += 4)
                {
                    int sno = BinaryPrimitives.ReadInt32LittleEndian(span[i..]);
                    if (!quests.TryGetValue(sno, out var uids)) continue;
                    uint phase = BinaryPrimitives.ReadUInt32LittleEndian(span[(i + 4)..]);
                    if (!uids.Contains(phase)) continue;
                    var candidate = new PhaseCandidate(address + i + 4, phase);
                    if (found.TryGetValue(sno, out var list)) { if (!list.Contains(candidate)) list.Add(candidate); } // windows overlap
                    else found[sno] = [candidate];
                }
            }
        return found.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<PhaseCandidate>)[.. kv.Value.OrderBy(c => c.Address)]);
    }

    /// <summary>The address to read a quest's phase at: the lowest pair holding a phase other than 0, else the lowest pair.</summary>
    public static long Pick(IReadOnlyList<PhaseCandidate> candidates) => Ordered(candidates).First().Address;

    private static IEnumerable<PhaseCandidate> Ordered(IEnumerable<PhaseCandidate> candidates) =>
        candidates.OrderBy(c => c.Phase == 0).ThenBy(c => c.Address);

    /// <summary>The phase UID at <paramref name="phaseAddress"/>; null when the pair no longer holds this quest and one of its phases.</summary>
    public static uint? Read(IMemoryReader memory, long phaseAddress, int questSno, IReadOnlySet<uint> phaseUids)
    {
        if (!PoolContainer.TryReadU32(memory, phaseAddress - sizeof(int), out uint sno) || (int)sno != questSno) return null;
        return PoolContainer.TryReadU32(memory, phaseAddress, out uint phase) && phaseUids.Contains(phase) ? phase : null;
    }
}
