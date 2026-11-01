using System.Numerics;
using System.Text.RegularExpressions;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>What a whisper asks for: most are plain whispers; a world boss's and a legion's stand in the table at every place they can rise.</summary>
public enum WhisperKind { Whisper, Dungeon, WorldBoss, Legion }

/// <summary>A whisper on the map: its quest, what it pays in grim favors (BountyDefinitions.dwBountyPoints), the world state and value that put
/// it up, its spot (QuestDefinition.vecStartLocation) and its subzone's internal name ("Hawe_Crossway"; null when it names none).</summary>
public sealed record Whisper(
    int QuestSno, string QuestName, WhisperKind Kind, int Favors, int WorldStateSno, int WorldStateValue, int WorldSno, Vector3 Position, string? Subzone)
{
    /// <summary>Whether the whisper is up in the rotation: its world state holds its value. A world boss's and a legion's hold it at every place,
    /// all the time (live 2026-11-01), so for them this names where one can rise, not where one does.</summary>
    public bool IsUp(IReadOnlyDictionary<int, int> worldStates) => worldStates.GetValueOrDefault(WorldStateSno) == WorldStateValue;
}

/// <summary>
/// The whispers (research/map-points.md §4b), from the same table as the helltide's bounties: the groups under
/// <c>Bounty_Unlock_{BaseGame,X1,X2}_{OW_Whispers,Dungeons,WorldEventsAndPvp}</c>. Left out: PvP, the conditions' breadcrumb and season
/// variants and a season's quests (QuestDefinition tRequirementsToBeActive.arSeasons), and any quest with no spot.
/// </summary>
public sealed partial class WhisperCatalog(SnoStore store, TypeSchema schema)
{
    private const string Group = "BountyGroups", Bounty = "BountyData", Quest = "QuestDefinition", Requirements = "RequirementsToBeActive",
        Balance = "GameBalanceDefinition", Definitions = "BountyDefinitions", DefinitionTable = "BountyDefinitionEntry_Table",
        Definition = "BountyDefinitionEntry", Header = "GBIDHeader";

    private IReadOnlyList<Whisper>? _all;

    public IReadOnlyList<Whisper> All => _all ??= Read();

    /// <summary>The quest's name in <paramref name="locale"/>, else its internal name.</summary>
    public string Name(Whisper whisper, string locale) => store.Text(locale, $"Quest_{whisper.QuestName}", "Name") ?? whisper.QuestName;

    [GeneratedRegex("^Bounty_Unlock_(BaseGame|X1|X2)_(OW_Whispers|Dungeons|WorldEventsAndPvp)$")]
    private static partial Regex WhisperCondition();

    private List<Whisper> Read()
    {
        var whispers = new List<Whisper>();
        if (BountyTable.Read(store, schema) is not { } read) return whispers;
        var (table, groups) = read;
        var favors = FavorsByDefinition();
        int questGroup = schema.SnoGroup(Quest);
        int location = schema.Offset(Quest, "vecStartLocation"), world = schema.Offset(Quest, "snoWorldStartLocation");
        int seasons = schema.Offset(Quest, "tRequirementsToBeActive") + schema.Offset(Requirements, "arSeasons");
        foreach (int group in groups)
        {
            if (store.Name(table.I32(group + schema.Offset(Group, "snoCondition"))) is not { } condition || !WhisperCondition().IsMatch(condition)) continue;
            foreach (int bounty in table.Array(group + schema.Offset(Group, "arBounties"), schema.Size(Bounty)))
            {
                int questSno = table.I32(bounty + schema.Offset(Bounty, "snoQuest"));
                if (store.Name(questSno) is not { } questName || KindOf(questName) is not { } kind) continue;
                if (store.Meta(questGroup, questSno) is not { } quest || quest.ArrayDescriptor(seasons).Size > 0) continue;
                var position = quest.Vec3(location);
                if (position == Vector3.Zero || !float.IsFinite(position.X)) continue;
                whispers.Add(new Whisper(questSno, questName, kind, favors.GetValueOrDefault(table.U32(bounty + schema.Offset(Bounty, "gbidBountyDefinition"))),
                    table.I32(bounty + schema.Offset(Bounty, "snoWorldState")), table.I32(bounty + schema.Offset(Bounty, "nValue")), quest.I32(world), position,
                    store.Name(table.I32(bounty + schema.Offset(Bounty, "snoBountySubzone")))));
            }
        }
        return [.. whispers.DistinctBy(w => w.QuestSno)];
    }

    // By the quest's name; null for PvP, which shares the world events' groups.
    private static WhisperKind? KindOf(string quest) =>
        quest.Contains("pvp", StringComparison.OrdinalIgnoreCase) ? null
        : quest.StartsWith("Bounty_WE_", StringComparison.Ordinal) ? WhisperKind.WorldBoss
        : quest.StartsWith("Bounty_ZE_", StringComparison.Ordinal) ? WhisperKind.Legion
        : quest.StartsWith("Bounty_Dungeon_", StringComparison.Ordinal) ? WhisperKind.Dungeon
        : WhisperKind.Whisper;

    // The game balance file BountyDefinitions: each kind of bounty's points, which the Tree of Whispers counts as grim favors.
    private Dictionary<uint, int> FavorsByDefinition()
    {
        var favors = new Dictionary<uint, int>();
        int balanceGroup = schema.SnoGroup(Balance);
        if (store.EntriesWithPrefix(balanceGroup, Definitions).Where(e => e.Name == Definitions).Select(e => store.Meta(balanceGroup, e.Id))
                .FirstOrDefault() is not { } file)
            return favors;
        int TypeAt(int p) => p + schema.Offset(DefinitionTable, "dwType");
        var contents = file.PolymorphicElements(schema.Offset(Balance, "ptData"),
            p => schema.TypeName(file.U32(TypeAt(p))) is { } t ? schema.Size(t) : null);
        if (contents?.Where(c => schema.TypeName(file.U32(TypeAt(c))) == DefinitionTable).Select(c => (int?)c).FirstOrDefault() is not { } content)
            return favors;
        foreach (int entry in file.Array(content + schema.Offset(DefinitionTable, "tEntries"), schema.Size(Definition)))
            favors[Gbid(file.InlineString(entry + schema.Offset(Definition, "tHeader") + schema.Offset(Header, "szName"), schema.Offset(Header, "gbid")))] =
                file.I32(entry + schema.Offset(Definition, "dwBountyPoints"));
        return favors;
    }

    // A game balance entry's id is the hash of its lowercased name: "Bounty_Type_Culling" is 946503055, as the quests name it.
    private static uint Gbid(string name)
    {
        uint hash = 0;
        foreach (char c in name) hash = unchecked(hash * 33 + char.ToLowerInvariant(c));
        return hash;
    }
}
