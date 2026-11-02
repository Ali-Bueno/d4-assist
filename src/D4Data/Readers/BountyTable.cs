using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>The Global <c>bounties</c> (GlobalBountyData.arBountyGroups, research/map-points.md §4): every helltide bounty and whisper of the
/// game, grouped under the condition that unlocks them.</summary>
internal static class BountyTable
{
    private const string GlobalDefinition = "GlobalDefinition", Name = "bounties", Data = "GlobalBountyData", Group = "BountyGroups";

    /// <summary>The table's blob and its groups' offsets in it; null when the game data has none.</summary>
    public static (SnoBlob Table, IReadOnlyList<int> Groups)? Read(SnoStore store, TypeSchema schema)
    {
        int globalGroup = schema.SnoGroup(GlobalDefinition);
        if (store.EntriesWithPrefix(globalGroup, Name).Where(e => e.Name == Name).Select(e => store.Meta(globalGroup, e.Id))
                .FirstOrDefault() is not { } table)
            return null;
        int TypeAt(int p) => p + schema.Offset(Data, "dwType");
        var contents = table.PolymorphicElements(schema.Offset(GlobalDefinition, "ptContent"),
            p => schema.TypeName(table.U32(TypeAt(p))) is { } t ? schema.Size(t) : null);
        if (contents?.Where(c => schema.TypeName(table.U32(TypeAt(c))) == Data).Select(c => (int?)c).FirstOrDefault() is not { } content)
            return null;
        return (table, [.. table.Array(content + schema.Offset(Data, "arBountyGroups"), schema.Size(Group))]);
    }
}
