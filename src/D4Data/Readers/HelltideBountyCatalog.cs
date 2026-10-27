using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A helltide bounty the map marks: its quest, its territory key ("Scos_DeepForest"), the world state that unlocks it, its
/// spot (QuestDefinition.vecStartLocation), and the Helltide_BountySubzone test that puts it on the map (its territory's turn).</summary>
public sealed record HelltideBounty(
    int QuestSno, string QuestName, string Territory, int WorldStateSno, int WorldSno, Vector3 Position, int SubzoneTest, int SubzoneValue)
{
    /// <summary>Whether the territory has the helltide's bounties while Helltide_BountySubzone is <paramref name="bountySubzone"/>:
    /// the condition's comparison 0 is equality, 5 at least (Scosglen: the Deep Forest == 1, the coast 5 with 2; live 2026-10-27,
    /// at 1 the map marked the Deep Forest's Blood Maiden, not the coast's).</summary>
    public bool HasTurn(int bountySubzone) => SubzoneTest == 5 ? bountySubzone >= SubzoneValue : bountySubzone == SubzoneValue;
}

/// <summary>
/// The helltide's Blood Maiden bounties (research/map-points.md §4): quests <c>Bounty_Helltide_Tier3_&lt;territory&gt;_Succuboss</c>
/// ("Tötet die Blutjungfrau"), one per helltide territory, each with its spot in the quest record. Every one's world state
/// <c>Bounty_Helltide_Tier2_&lt;territory&gt;_Succuboss</c> stays 1 (live 2026-10-27, all regions); which territory has the
/// bounties is the world state Helltide_BountySubzone, tested by <c>Bounty_Unlock_Condition_Helltide_&lt;territory&gt;</c> beside
/// UberSubzone. Fields of Hatred (PVP) territories are left out (their tests overlap the region's). Other helltide bounties carry
/// spots too, but their quests and world states are named apart (CullDemons, ChestsAny…), so they are left out for now.
/// </summary>
public sealed class HelltideBountyCatalog(SnoStore store, TypeSchema schema)
{
    private const string Prefix = "Bounty_Helltide_", Suffix = "_Succuboss", UnlockPrefix = "Bounty_Unlock_Condition_Helltide";
    private const string BountySubzoneState = "Helltide_BountySubzone";

    // In a condition's subcondition the comparison sits two words before the world state's SNO, the value right after it.
    private const int ComparisonBefore = 8, ValueAfter = 4;

    private IReadOnlyList<HelltideBounty>? _all;

    public IReadOnlyList<HelltideBounty> All => _all ??= Read();

    /// <summary>The world state whose value says which territory of the helltide has the bounties; -1 when unknown.</summary>
    public int BountySubzoneSno => _bountySubzone ??= store.EntriesWithPrefix(schema.SnoGroup("WorldStateDefinition"), BountySubzoneState)
        .Where(e => e.Name == BountySubzoneState).Select(e => e.Id).DefaultIfEmpty(-1).First();

    private int? _bountySubzone;

    /// <summary>The quest's name in <paramref name="locale"/> ("Tötet die Blutjungfrau"), else its internal name.</summary>
    public string Name(HelltideBounty bounty, string locale) => store.Text(locale, $"Quest_{bounty.QuestName}", "Name") ?? bounty.QuestName;

    private List<HelltideBounty> Read()
    {
        int questGroup = schema.SnoGroup("QuestDefinition"), stateGroup = schema.SnoGroup("WorldStateDefinition");
        int location = schema.Offset("QuestDefinition", "vecStartLocation"), world = schema.Offset("QuestDefinition", "snoWorldStartLocation");
        var states = store.EntriesWithPrefix(stateGroup, Prefix).Where(e => e.Name.EndsWith(Suffix, StringComparison.Ordinal))
            .GroupBy(e => Territory(e.Name)).ToDictionary(g => g.Key, g => g.First().Id);
        var turns = Turns();
        var bounties = new List<HelltideBounty>();
        foreach (var (id, name) in store.EntriesWithPrefix(questGroup, Prefix))
        {
            if (!name.EndsWith(Suffix, StringComparison.Ordinal)) continue;
            string territory = Territory(name);
            if (territory.Contains("PVP", StringComparison.OrdinalIgnoreCase)) continue;
            // The conditions say "Hawe_Wetland" where quest and world state say "Hawe_Wetlands".
            if (!states.TryGetValue(territory, out int state) ||
                !turns.TryGetValue(territory, out var turn) && !turns.TryGetValue(territory.TrimEnd('s'), out turn)) continue;
            if (store.Meta(questGroup, id) is not { } quest) continue;
            var position = quest.Vec3(location);
            if (position == Vector3.Zero || !float.IsFinite(position.X)) continue;
            bounties.Add(new HelltideBounty(id, name, territory, state, quest.I32(world), position, turn.Test, turn.Value));
        }
        return bounties;
    }

    // Each territory's Helltide_BountySubzone test, from Bounty_Unlock_Condition_Helltide[_]<territory> (not its _S11 / _Eternal
    // variants): "Bounty_Unlock_Condition_HelltideScos_Coast" → Scos_Coast: at least 2.
    private Dictionary<string, (int Test, int Value)> Turns()
    {
        var turns = new Dictionary<string, (int, int)>();
        int conditionGroup = schema.SnoGroup("ConditionDefinition"), subzone = BountySubzoneSno;
        if (subzone <= 0) return turns;
        foreach (var (id, name) in store.EntriesWithPrefix(conditionGroup, UnlockPrefix))
        {
            string territory = name[UnlockPrefix.Length..].TrimStart('_');
            if (territory.EndsWith("_S11", StringComparison.Ordinal) || territory.EndsWith("_Eternal", StringComparison.Ordinal)) continue;
            if (store.Meta(conditionGroup, id) is not { } condition) continue;
            for (int at = ComparisonBefore; at + ValueAfter + 4 <= condition.Length; at += 4)
                if (condition.I32(at) == subzone)
                {
                    turns[territory] = (condition.I32(at - ComparisonBefore), condition.I32(at + ValueAfter));
                    break;
                }
        }
        return turns;
    }

    // "Bounty_Helltide_Tier3_Scos_DeepForest_Succuboss" → "Scos_DeepForest".
    private static string Territory(string name)
    {
        string rest = name[Prefix.Length..^Suffix.Length];
        return rest.StartsWith("Tier", StringComparison.Ordinal) && rest.IndexOf('_') is > 0 and var tier ? rest[(tier + 1)..] : rest;
    }
}
