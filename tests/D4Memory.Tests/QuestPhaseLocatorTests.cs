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
}
