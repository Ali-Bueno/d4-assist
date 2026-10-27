using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A helltide bounty the map marks: its quest, its territory key ("Scos_DeepForest"), the world state the server sets to 1
/// while it is up, and its spot (QuestDefinition.vecStartLocation).</summary>
public sealed record HelltideBounty(int QuestSno, string QuestName, string Territory, int WorldStateSno, int WorldSno, Vector3 Position);

/// <summary>
/// The helltide's Blood Maiden bounties (research/map-points.md §4): quests <c>Bounty_Helltide_Tier3_&lt;territory&gt;_Succuboss</c>
/// ("Tötet die Blutjungfrau"), one per helltide territory, each with its spot in the quest record; the server marks one up with
/// the world state <c>Bounty_Helltide_Tier2_&lt;territory&gt;_Succuboss</c> = 1 (live 2026-10-27, Scosglen: the map centered on
/// the Deep Forest one at (-1303.9, -1002.2)). Other helltide bounties carry spots too, but their quests and world states are named
/// apart (CullDemons, ChestsAny…), so they are left out until the link between them is known.
/// </summary>
public sealed class HelltideBountyCatalog(SnoStore store, TypeSchema schema)
{
    private const string Prefix = "Bounty_Helltide_", Suffix = "_Succuboss";

    private IReadOnlyList<HelltideBounty>? _all;

    public IReadOnlyList<HelltideBounty> All => _all ??= Read();

    /// <summary>The quest's name in <paramref name="locale"/> ("Tötet die Blutjungfrau"), else its internal name.</summary>
    public string Name(HelltideBounty bounty, string locale) => store.Text(locale, $"Quest_{bounty.QuestName}", "Name") ?? bounty.QuestName;

    private List<HelltideBounty> Read()
    {
        int questGroup = schema.SnoGroup("QuestDefinition"), stateGroup = schema.SnoGroup("WorldStateDefinition");
        int location = schema.Offset("QuestDefinition", "vecStartLocation"), world = schema.Offset("QuestDefinition", "snoWorldStartLocation");
        var states = store.EntriesWithPrefix(stateGroup, Prefix).Where(e => e.Name.EndsWith(Suffix, StringComparison.Ordinal))
            .GroupBy(e => Territory(e.Name)).ToDictionary(g => g.Key, g => g.First().Id);
        var bounties = new List<HelltideBounty>();
        foreach (var (id, name) in store.EntriesWithPrefix(questGroup, Prefix))
        {
            if (!name.EndsWith(Suffix, StringComparison.Ordinal) || !states.TryGetValue(Territory(name), out int state)) continue;
            if (store.Meta(questGroup, id) is not { } quest) continue;
            var position = quest.Vec3(location);
            if (position == Vector3.Zero || !float.IsFinite(position.X)) continue;
            bounties.Add(new HelltideBounty(id, name, Territory(name), state, quest.I32(world), position));
        }
        return bounties;
    }

    // "Bounty_Helltide_Tier3_Scos_DeepForest_Succuboss" → "Scos_DeepForest".
    private static string Territory(string name)
    {
        string rest = name[Prefix.Length..^Suffix.Length];
        return rest.StartsWith("Tier", StringComparison.Ordinal) && rest.IndexOf('_') is > 0 and var tier ? rest[(tier + 1)..] : rest;
    }
}
