using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A helltide bounty the map marks: its quest, its territory key ("Scos_DeepForest", from its unlock condition), the world
/// state and value that put it up, its spot (QuestDefinition.vecStartLocation), and the Helltide_BountySubzone test that gives its
/// territory the helltide's bounties.</summary>
public sealed record HelltideBounty(
    int QuestSno, string QuestName, string Territory, int WorldStateSno, int WorldStateValue, int WorldSno, Vector3 Position,
    int SubzoneTest, int SubzoneValue)
{
    /// <summary>Whether the territory has the helltide's bounties while Helltide_BountySubzone is <paramref name="bountySubzone"/>:
    /// the condition's comparison 0 is equality, 5 at least (Scosglen: the Deep Forest == 1, the coast 5 with 2; live 2026-10-27,
    /// at 1 the map marked the Deep Forest's Blood Maiden, not the coast's).</summary>
    public bool HasTurn(int bountySubzone) => SubzoneTest == 5 ? bountySubzone >= SubzoneValue : bountySubzone == SubzoneValue;

    /// <summary>Whether the bounty is up in the rotation: its world state holds its value.</summary>
    public bool IsUp(IReadOnlyDictionary<int, int> worldStates) => worldStates.GetValueOrDefault(WorldStateSno) == WorldStateValue;
}

/// <summary>
/// The helltide's bounties (research/map-points.md §4), from the game's own table: the Global <c>bounties</c>
/// (GlobalBountyData.arBountyGroups) groups each territory's bounties under its unlock condition
/// <c>Bounty_Unlock_Condition_Helltide[_]&lt;territory&gt;</c>, each bounty a quest (<c>Bounty_Helltide_Tier&lt;n&gt;_&lt;territory&gt;_&lt;kind&gt;</c>:
/// cull cultists, cull demons, cultist rituals, doomsayers, harbingers, Ironwolves, tortured gifts, cinders, demonic structures,
/// the Blood Maiden) with the world state and value that put it up. The world states roll over on the hour and are named apart
/// from the quests (Tier1_Kill puts up CullCultist, Tier2_Kill CullDemons). Which territory has the bounties is the world state
/// Helltide_BountySubzone, tested by the unlock condition beside UberSubzone. Left out: Fields of Hatred (PVP) territories (their
/// tests overlap the region's), the conditions' _S11 / _Eternal variants, and a season's bounties (QuestDefinition
/// tRequirementsToBeActive.arSeasons: S10 chaos rifts, S12 kill streaks, S14 ruptures).
/// </summary>
public sealed class HelltideBountyCatalog(SnoStore store, TypeSchema schema)
{
    private const string UnlockPrefix = "Bounty_Unlock_Condition_Helltide", BountySubzoneState = "Helltide_BountySubzone";
    private const string GlobalDefinition = "GlobalDefinition", BountyTable = "bounties", BountyData = "GlobalBountyData",
        Group = "BountyGroups", Bounty = "BountyData", Quest = "QuestDefinition", Requirements = "RequirementsToBeActive";

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
        var bounties = new List<HelltideBounty>();
        int globalGroup = schema.SnoGroup(GlobalDefinition);
        if (store.EntriesWithPrefix(globalGroup, BountyTable).Where(e => e.Name == BountyTable).Select(e => store.Meta(globalGroup, e.Id))
                .FirstOrDefault() is not { } table)
            return bounties;
        int TypeAt(int p) => p + schema.Offset(BountyData, "dwType");
        var contents = table.PolymorphicElements(schema.Offset(GlobalDefinition, "ptContent"),
            p => schema.TypeName(table.U32(TypeAt(p))) is { } t ? schema.Size(t) : null);
        if (contents?.Where(c => schema.TypeName(table.U32(TypeAt(c))) == BountyData).Select(c => (int?)c).FirstOrDefault() is not { } content)
            return bounties;
        int conditionGroup = schema.SnoGroup("ConditionDefinition"), questGroup = schema.SnoGroup(Quest);
        int location = schema.Offset(Quest, "vecStartLocation"), world = schema.Offset(Quest, "snoWorldStartLocation");
        int seasons = schema.Offset(Quest, "tRequirementsToBeActive") + schema.Offset(Requirements, "arSeasons");
        foreach (int group in table.Array(content + schema.Offset(BountyData, "arBountyGroups"), schema.Size(Group)))
        {
            int condition = table.I32(group + schema.Offset(Group, "snoCondition"));
            if (store.Name(condition) is not { } conditionName || Territory(conditionName) is not { } territory
                || Turn(conditionGroup, condition) is not { } turn)
                continue;
            foreach (int bounty in table.Array(group + schema.Offset(Group, "arBounties"), schema.Size(Bounty)))
            {
                int questSno = table.I32(bounty + schema.Offset(Bounty, "snoQuest"));
                if (store.Name(questSno) is not { } questName || store.Meta(questGroup, questSno) is not { } quest) continue;
                if (quest.ArrayDescriptor(seasons).Size > 0) continue; // a season's bounty
                var position = quest.Vec3(location);
                if (position == Vector3.Zero || !float.IsFinite(position.X)) continue;
                bounties.Add(new HelltideBounty(questSno, questName, territory, table.I32(bounty + schema.Offset(Bounty, "snoWorldState")),
                    table.I32(bounty + schema.Offset(Bounty, "nValue")), quest.I32(world), position, turn.Test, turn.Value));
            }
        }
        return bounties;
    }

    // A helltide territory's unlock condition, "Bounty_Unlock_Condition_HelltideScos_Coast" → "Scos_Coast"; null for another
    // condition, a Fields of Hatred one, or an _S11 / _Eternal variant.
    private static string? Territory(string condition)
    {
        if (!condition.StartsWith(UnlockPrefix, StringComparison.Ordinal)) return null;
        string territory = condition[UnlockPrefix.Length..].TrimStart('_');
        return territory.Length == 0 || territory.Contains("PVP", StringComparison.OrdinalIgnoreCase)
            || territory.EndsWith("_S11", StringComparison.Ordinal) || territory.EndsWith("_Eternal", StringComparison.Ordinal) ? null : territory;
    }

    // The condition's Helltide_BountySubzone test: "Bounty_Unlock_Condition_HelltideScos_Coast" → at least 2.
    private (int Test, int Value)? Turn(int conditionGroup, int condition)
    {
        int subzone = BountySubzoneSno;
        if (subzone <= 0 || store.Meta(conditionGroup, condition) is not { } data) return null;
        for (int at = ComparisonBefore; at + ValueAfter + 4 <= data.Length; at += 4)
            if (data.I32(at) == subzone)
                return (data.I32(at - ComparisonBefore), data.I32(at + ValueAfter));
        return null;
    }
}
