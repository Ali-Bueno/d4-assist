using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>
/// The lowest world tier a ConditionDefinition asks for: its <c>WorldTierSubcondition</c> (the game's tier) and
/// <c>PlayerWorldTierSubcondition</c> (the player's) tests of "at least N" or "exactly N" that must all hold. eWorldTier counts
/// 0 Normal, 1 Hard, 2 Expert, 3 Penitent, 4 Torment 1 … 15 Torment 12 (the conditions <c>WorldTier_IsWorldTier_Torment_1</c> = 4,
/// <c>WorldTier_ItemSource_Torment_12orGreater</c> = 15; live 2026-11-07, research/client-memory.md §4, world tier).
/// </summary>
public sealed class WorldTierCondition(SnoStore store, TypeSchema schema)
{
    /// <summary>eWorldTier of Torment 1, the first Torment tier.</summary>
    public const int FirstTorment = 4;

    private const string Definition = "ConditionDefinition", All = "AllLogicSubcondition", Any = "AnyLogicSubcondition";
    private static readonly string[] TierTests = ["WorldTierSubcondition", "PlayerWorldTierSubcondition"];
    // eComparisonOp: 0 equal, 5 at least (WorldTier_IsWorldTier_Torment_2 = equal 5, …_Torment_2orGreater = at least 5).
    private const int EqualOp = 0, AtLeastOp = 5;

    private readonly Dictionary<int, int> _known = [];

    /// <summary>The tier the condition needs at least, 0 for a condition that needs none (or none readable).</summary>
    public int MinimumOf(int conditionSno)
    {
        if (conditionSno <= 0) return 0;
        if (_known.TryGetValue(conditionSno, out int tier)) return tier;
        tier = store.Meta(schema.SnoGroup(Definition), conditionSno) is { } data
            ? Minimum(data, schema.Offset(Definition, "ptRootCondition")) : 0;
        return _known[conditionSno] = tier;
    }

    // Every element of the array must hold (the root holds one); an "all" holds when each child does, an "any" with one child is
    // that child. Any other "any" or a negated test gives no floor.
    private int Minimum(SnoBlob data, int array)
    {
        int typeOffset = schema.Offset(All, "dwType");
        var elements = data.PolymorphicElements(array,
            p => p + typeOffset + sizeof(uint) <= data.Length && schema.TypeName(data.U32(p + typeOffset)) is { } t ? schema.Size(t) : null);
        if (elements is null) return 0;
        int floor = 0;
        foreach (int p in elements)
        {
            string type = schema.TypeName(data.U32(p + typeOffset))!;
            int Field(string name) => data.I32(p + schema.Offset(type, name));
            if (Field("bNegate") != 0) continue;
            if (TierTests.Contains(type) && Field("eComparisonOp") is EqualOp or AtLeastOp)
                floor = Math.Max(floor, Field("eWorldTier"));
            else if (type == All)
                floor = Math.Max(floor, Minimum(data, p + schema.Offset(All, "arConditions")));
            else if (type == Any && data.PolymorphicElements(p + schema.Offset(Any, "arConditions"),
                         q => q + typeOffset + sizeof(uint) <= data.Length && schema.TypeName(data.U32(q + typeOffset)) is { } t ? schema.Size(t) : null) is [_])
                floor = Math.Max(floor, Minimum(data, p + schema.Offset(Any, "arConditions")));
        }
        return floor;
    }
}
