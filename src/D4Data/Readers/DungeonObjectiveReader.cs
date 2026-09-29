using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>The actor SNOs a dungeon's DPO objective quest names, from the world SNO (research/dungeon-objectives.md §1-2).</summary>
public sealed class DungeonObjectiveReader(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", QuestDefinition = "QuestDefinition", ActorDefinition = "ActorDefinition";
    private const string ZoneProgressGlobal = "zone_progress";

    private readonly DpoLayout _layout = DpoLayout.From(schema);
    private readonly Dictionary<int, IReadOnlySet<int>> _cache = [];
    private Dictionary<int, int[]>? _worldQuests;

    /// <summary>Empty for a world without a DPO quest, or when the Global record is unreadable.</summary>
    public IReadOnlySet<int> ActorSnos(int worldSno)
    {
        if (_cache.TryGetValue(worldSno, out var cached)) return cached;
        var actors = new HashSet<int>();
        int actorGroup = schema.SnoGroup(ActorDefinition), questGroup = schema.SnoGroup(QuestDefinition);
        if (WorldQuests().TryGetValue(worldSno, out var quests))
            foreach (int quest in quests)
                if (store.Meta(questGroup, quest) is { } blob)
                    foreach (var (group, id) in DungeonObjectiveParser.QuestTokens(blob, _layout))
                        if (group == actorGroup) actors.Add(id);
        return _cache[worldSno] = actors;
    }

    private Dictionary<int, int[]> WorldQuests()
    {
        if (_worldQuests is not null) return _worldQuests;
        int group = schema.SnoGroup(GlobalDefinition);
        return _worldQuests = store.Meta(group, store.Resolve(group, ZoneProgressGlobal)) is { } global
            ? DungeonObjectiveParser.WorldQuests(global, _layout) : [];
    }
}
