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
    int QuestSno, string QuestName, WhisperKind Kind, int Favors, int WorldStateSno, int WorldStateValue, int WorldSno, Vector3 Position, string? Subzone,
    int RegionEventStateSno = 0)
{
    /// <summary>Whether the whisper is up in the rotation: its world state holds its value. A world boss's and a legion's hold it at every place,
    /// all the time (live 2026-11-01), so for them this names where one can rise; the map's bounty table says where one does (MapBountyTableReader).</summary>
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

    /// <summary>The whispers to list now. A plain one while its world state puts it up. A legion only where the open map holds its pin
    /// (<paramref name="pins"/>, MapBountyTableReader). A world boss likewise, and, once one has a pin, also where its region's
    /// <c>WE_Sanc_&lt;region&gt;</c> is set: the table may pin one of its two places only, and the state alone stays on the last boss
    /// (research/map-points.md §4b).</summary>
    public IReadOnlyList<Whisper> Listed(IReadOnlyDictionary<int, int> worldStates, IReadOnlySet<int> pins)
    {
        bool bossHeld = All.Any(w => w.Kind == WhisperKind.WorldBoss && pins.Contains(w.QuestSno));
        return [.. All.Where(w => w.IsUp(worldStates) && w.Kind switch
        {
            WhisperKind.Legion => pins.Contains(w.QuestSno),
            WhisperKind.WorldBoss => pins.Contains(w.QuestSno) || (bossHeld && worldStates.GetValueOrDefault(w.RegionEventStateSno) != 0),
            _ => true,
        })];
    }

    /// <summary>The legions and world bosses up now, for <see cref="Listed"/>: where the client holds an event object (<paramref name="eventUp"/>, from its
    /// warmup until shortly after its start). A legion's pins keep past places and miss coming ones; a world boss's pin counts, as it goes when the boss
    /// falls (research/map-points.md §4b).</summary>
    public IReadOnlySet<int> EventsUp(IReadOnlySet<int> pins, Func<Whisper, bool> eventUp) =>
        All.Where(w => w.Kind switch
        {
            WhisperKind.Legion => eventUp(w),
            WhisperKind.WorldBoss => eventUp(w) || pins.Contains(w.QuestSno),
            _ => false,
        }).Select(w => w.QuestSno).ToHashSet();

    /// <summary>The minutes until a whisper's event starts, by <paramref name="ofEvent"/> at its own spot. World bosses rise together, and the client may hold an event
    /// object for only one of them (19:30 on 2026-11-01: Scosglen, not Skovos), so a world boss without its own takes another's; null for anything else without one.</summary>
    public int? MinutesToStart(Whisper whisper, Func<Whisper, int?> ofEvent) =>
        ofEvent(whisper) ?? (whisper.Kind == WhisperKind.WorldBoss ? All.Where(w => w.Kind == WhisperKind.WorldBoss).Select(ofEvent).FirstOrDefault(m => m is not null) : null);

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
                    store.Name(table.I32(bounty + schema.Offset(Bounty, "snoBountySubzone"))), kind == WhisperKind.WorldBoss ? RegionEventState(questName) : 0));
            }
        }
        return [.. whispers.DistinctBy(w => w.QuestSno)];
    }

    // "Bounty_WE_Tier3_Skov_Boss" -> the world state WE_Sanc_Skov; 0 when there is none.
    private int RegionEventState(string quest)
    {
        if (quest.Split('_') is not { Length: > 3 } parts) return 0;
        string state = "WE_Sanc_" + parts[3];
        return store.EntriesWithPrefix(schema.SnoGroup("WorldStateDefinition"), state).Where(e => e.Name == state).Select(e => e.Id).FirstOrDefault();
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
        // The name runs up to the header's gbid field, which the file leaves at -1: the id is hashed from the name.
        int nameBytes = schema.Offset(Header, "gbid") - schema.Offset(Header, "szName");
        foreach (int entry in file.Array(content + schema.Offset(DefinitionTable, "tEntries"), schema.Size(Definition)))
            favors[Gbid(file.InlineString(entry + schema.Offset(Definition, "tHeader") + schema.Offset(Header, "szName"), nameBytes))] =
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
