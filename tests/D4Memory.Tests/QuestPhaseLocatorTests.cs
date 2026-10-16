using System.Buffers.Binary;
using D4Memory.Actors;

namespace D4Memory.Tests;

public class QuestPhaseLocatorTests
{
    private const int Underworld2 = 549310, Vault = 549147, NotStarted = 486210;
    private const long Base = 0x10000;

    private static byte[] Pair(byte[] data, int at, int quest, uint phase)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), quest);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at + 4), phase);
        return data;
    }

    [Fact]
    public void FindAll_ReturnsEveryQuestInProgressWithItsLowestPhaseAddress()
    {
        var data = new byte[0x400];
        Pair(data, 0x200, Underworld2, 80);
        Pair(data, 0x100, Underworld2, 80); // a second copy lower down: the lowest one wins, as Find's first hit
        Pair(data, 0x300, Vault, 87);
        Pair(data, 0x40, NotStarted, 999);  // not one of its phases
        var memory = new FakeMemoryReader();
        memory.AddRegion(Base, data);
        var quests = new Dictionary<int, IReadOnlySet<uint>>
        {
            [Underworld2] = new HashSet<uint> { 77, 80 },
            [Vault] = new HashSet<uint> { 87, 277 },
            [NotStarted] = new HashSet<uint> { 236, 271 },
        };

        var found = QuestPhaseLocator.FindAll(memory, memory.Regions(), quests);

        Assert.Equal(2, found.Count);
        Assert.Equal(Base + 0x104, found[Underworld2]);
        Assert.Equal(Base + 0x304, found[Vault]);
        Assert.Equal(80u, QuestPhaseLocator.Read(memory, found[Underworld2], Underworld2, quests[Underworld2]));
    }

    private const int LostArchives = 582753; // DPO_Frac_LostArchives: 39 kill → 53 boss room → 0 carry → 25 coffin → 5 miniboss
    private static readonly IReadOnlySet<uint> LostArchivesPhases = new HashSet<uint> { 0, 5, 25, 33, 39, 53, 56 };

    [Fact]
    public void A_stray_quest_and_zero_pair_lower_down_does_not_win_over_the_pair_holding_the_phase()
    {
        // Live 2026-10-16: "582753 at phase 0" from the entrance on, and no phase change for the whole dungeon.
        var data = new byte[0x400];
        Pair(data, 0x80, LostArchives, 0);   // the SNO and a zero word that happen to sit side by side
        Pair(data, 0x300, LostArchives, 39); // the real pair: the kill step
        var memory = new FakeMemoryReader();
        memory.AddRegion(Base, data);
        var quests = new Dictionary<int, IReadOnlySet<uint>> { [LostArchives] = LostArchivesPhases };

        long at = QuestPhaseLocator.FindAll(memory, memory.Regions(), quests)[LostArchives];

        Assert.Equal(Base + 0x304, at);
        Assert.Equal(Base + 0x304, QuestPhaseLocator.Find(memory, memory.Regions(), LostArchives, LostArchivesPhases)[0]);
        Assert.Equal(2, QuestPhaseLocator.FindCandidates(memory, memory.Regions(), LostArchives, LostArchivesPhases).Count);

        Pair(data, 0x300, LostArchives, 0); // the carry step: read at the same spot, the quest goes on into phase 0
        Assert.Equal(0u, QuestPhaseLocator.Read(memory, at, LostArchives, LostArchivesPhases));
    }

    [Fact]
    public void A_quest_at_phase_zero_with_no_other_pair_is_read_there()
    {
        var data = new byte[0x400];
        Pair(data, 0x200, LostArchives, 0);
        Pair(data, 0x280, LostArchives, 0);
        var memory = new FakeMemoryReader();
        memory.AddRegion(Base, data);

        var found = QuestPhaseLocator.FindAll(memory, memory.Regions(), new Dictionary<int, IReadOnlySet<uint>> { [LostArchives] = LostArchivesPhases });

        Assert.Equal(Base + 0x204, found[LostArchives]);
    }
}
