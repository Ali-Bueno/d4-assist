using System.Buffers.Binary;
using D4Memory.Ui;

namespace D4Memory.Tests;

public class ConversationReaderTests
{
    private const long ModuleBase = 0x7FF700000000, Node = 0x1EC00001000;
    // Build 3.2.1.73552 layout (GameBuildOffsetsTable).
    private static readonly ConversationOffsets Offsets = new(
        OptionsPointerRva: 0x31326A0, OptionStride: 0x20, OptionSnoOffset: 0x10, PlayingListRva: 0x30D28F8, NodeSnoOffset: 0x28);
    private const long OptionsRva = 0x3132400;

    [Fact]
    public void ClosedMenuAndEmptyList_ReadEmpty()
    {
        var memory = Module(optionCount: 0, playingSno: null);

        var snapshot = ConversationReader.TryRead(memory, ModuleBase, Offsets)!;

        Assert.Empty(snapshot.MenuOptions);
        Assert.Empty(snapshot.Playing);
    }

    [Fact]
    public void OpenMenuAndAPlayingNode_ReadTheirSnos()
    {
        var memory = Module(optionCount: 2, playingSno: 843567);

        var snapshot = ConversationReader.TryRead(memory, ModuleBase, Offsets)!;

        Assert.Equal([641998, 843567], snapshot.MenuOptions);
        Assert.Equal([843567], snapshot.Playing);
    }

    private static FakeMemoryReader Module(uint optionCount, int? playingSno)
    {
        long head = ModuleBase + Offsets.PlayingListRva;
        var data = new byte[0x3140000];
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan((int)Offsets.OptionsPointerRva), ModuleBase + OptionsRva);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan((int)Offsets.OptionsPointerRva + 8), optionCount);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan((int)OptionsRva + Offsets.OptionSnoOffset), 641998);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan((int)OptionsRva + Offsets.OptionStride + Offsets.OptionSnoOffset), 843567);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan((int)Offsets.PlayingListRva), playingSno is null ? head : Node);

        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase, data);
        if (playingSno is { } sno)
        {
            var node = new byte[0x40];
            BinaryPrimitives.WriteInt64LittleEndian(node, head); // next: back to the head
            BinaryPrimitives.WriteInt32LittleEndian(node.AsSpan(Offsets.NodeSnoOffset), sno);
            memory.AddRegion(Node, node);
        }
        return memory;
    }
}
