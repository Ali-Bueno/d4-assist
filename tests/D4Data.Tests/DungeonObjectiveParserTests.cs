using System.Buffers.Binary;
using D4Data.Format;
using D4Data.Readers;

namespace D4Data.Tests;

public class DungeonObjectiveParserTests
{
    private const int ContentField = 8, ZoneProgressSize = 64, TypeHeader = 8, ListField = 16, EntryStride = 24;
    private const int ActorGroup = 1, OtherGroup = 31;

    // Only the fields the parsers read matter; the offsets are arbitrary but distinct.
    private static readonly DpoLayout Layout = new(
        GlobalContentField: ContentField, ZoneProgressSize: ZoneProgressSize, WorldQuestListField: ListField, WorldQuestStride: EntryStride,
        WorldQuestWorldField: 0, WorldQuestQuestsField: 8,
        QuestPhasesField: 0, PhaseStride: 16, PhaseCallbackSetsField: 0, CallbackSetStride: 16, SetCallbacksField: 0, CallbackStride: 16,
        CallbackTokensField: 0, SnoNameStride: 8);

    private sealed class Body
    {
        private readonly byte[] _bytes = new byte[512];
        public Body Int(int off, int v) { BinaryPrimitives.WriteInt32LittleEndian(_bytes.AsSpan(off), v); return this; }
        /// <summary>DT_VARIABLEARRAY descriptor for the field at <paramref name="field"/>: data offset and size at +8 / +12.</summary>
        public Body Array(int field, int data, int size) => Int(field + 8, data).Int(field + 12, size);
        /// <summary>DT_POLYMORPHIC_VARIABLEARRAY descriptor: like <see cref="Array"/> plus the count at +16; data holds one 8-byte type header per element.</summary>
        public Body PolymorphicArray(int field, int data, int count, int stride) =>
            Array(field, data, count * (TypeHeader + stride)).Int(field + 16, count);
        public SnoBlob Blob()
        {
            var file = new byte[SnoBlob.SnoHeaderSize + _bytes.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(file, SnoBlob.SnoHeaderMagic);
            _bytes.CopyTo(file, SnoBlob.SnoHeaderSize);
            return SnoBlob.FromMeta(file);
        }
    }

    [Fact]
    public void WorldQuests_SkipsTheTypeHeaderAndReadsEachWorldsQuests()
    {
        const int data = 32, content = data + TypeHeader, entries = 100, quests0 = 200, quests1 = 210;
        var body = new Body()
            .PolymorphicArray(ContentField, data, count: 1, ZoneProgressSize)
            .Array(content + ListField, entries, 2 * EntryStride)
            .Int(entries, 111).Array(entries + 8, quests0, 8).Int(quests0, 5).Int(quests0 + 4, 6)
            .Int(entries + EntryStride, 222).Array(entries + EntryStride + 8, quests1, 4).Int(quests1, 7);

        var map = DungeonObjectiveParser.WorldQuests(body.Blob(), Layout);

        Assert.Equal([5, 6], map[111]);
        Assert.Equal([7], map[222]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void WorldQuests_IsEmptyWhenTheContentIsMissingOrItsSizeIsNotTheBaseStruct()
    {
        Assert.Empty(DungeonObjectiveParser.WorldQuests(new Body().Blob(), Layout));
        Assert.Empty(DungeonObjectiveParser.WorldQuests(new Body().PolymorphicArray(ContentField, 32, count: 1, ZoneProgressSize - 1).Int(ContentField + 12, ZoneProgressSize + TypeHeader).Blob(), Layout));
    }

    [Fact]
    public void QuestTokens_WalksPhasesSetsCallbacksAndTokens()
    {
        const int phases = 100, sets = 130, callbacks = 150, tokens = 170;
        var body = new Body()
            .PolymorphicArray(0, phases, count: 1, stride: 16)
            .Array(phases + TypeHeader, sets, 16)
            .Array(sets, callbacks, 16)
            .Array(callbacks, tokens, 16)
            .Int(tokens, ActorGroup).Int(tokens + 4, 1120667)
            .Int(tokens + 8, OtherGroup).Int(tokens + 12, 42);

        var found = DungeonObjectiveParser.QuestTokens(body.Blob(), Layout).ToList();

        Assert.Equal([(ActorGroup, 1120667), (OtherGroup, 42)], found);
    }

    [Fact]
    public void QuestTokens_IsEmptyForAQuestWithoutPhases() =>
        Assert.Empty(DungeonObjectiveParser.QuestTokens(new Body().Blob(), Layout));
}
