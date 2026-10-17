using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A marker in a scene's own layer set (<c>&lt;Scene&gt; (&lt;Layer&gt;)</c>): its position is scene-local, so it lands in the
/// world only where that scene is placed (a DRLG dungeon's tile, live 2026-10-01: the Ancient Mausoleum's central chamber).</summary>
public sealed record SceneMarker(int SceneSno, PlacedMarker Marker);

/// <summary>One quest phase's objects: the markers its scripts name in world space and in scenes, and the actor SNOs its callbacks name.</summary>
/// <param name="SubQuests">The quests the phase waits on (ScriptEventParamQuest): the game then shows their phase instead of this one's
/// (live 2026-10-04: S15_QST_Main_03_TriadC_01 phase 81 hands over to the hub's quest S15_Triad_C_Travincal). Null for none.</param>
/// <param name="Worlds">The worlds the phase's callbacks show their step in (QuestCallback.snoWorld): QST_Frac_PaleKnightsKin's
/// phase 152 (find the journals) shows DGN_Frac_LightsWatch, phase 15 (bring them back) Sanctuary_Eastern_Continent. Null for none.</param>
public sealed record QuestPhaseObjects(
    uint PhaseUid, IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers,
    IReadOnlySet<int>? SubQuests = null, IReadOnlySet<int>? Worlds = null);

/// <summary>The objects a quest's objective scripts name (research/client-memory.md §4, quest gizmos).</summary>
/// <remarks>Walks phases → objective sets → callbacks, keeping every ScriptEventParamMarkerHandle of the script message map's
/// parameters and event filters (resolved through its marker set), every ScriptEventParamQuest there (a sub-quest) and every
/// actor in the callback's <c>arSnonameTokens</c>. Cached per quest; not thread-safe.</remarks>
public sealed class QuestScriptMarkerReader(SnoStore store, TypeSchema schema, MarkerReader markers)
{
    private const string Quest = "QuestDefinition", Phase = "QuestPhase", Set = "QuestObjectiveSet", Callback = "QuestCallback";
    private const string ActorDefinition = "ActorDefinition", SnoName = "DT_SNO_NAME", SceneDefinition = "SceneDefinition";
    private const string MessageMap = "ScriptMessageMap", Filter = "ScriptMessageMapFilter";
    private const string Param = "ScriptEventParamBase", MarkerHandleParam = "ScriptEventParamMarkerHandle", QuestParam = "ScriptEventParamQuest";

    private readonly Dictionary<int, IReadOnlyList<PlacedMarker>> _cache = [];
    private readonly Dictionary<int, IReadOnlyList<QuestPhaseObjects>> _phaseCache = [];
    private Dictionary<string, List<int>>? _questsByFrame;
    private Dictionary<int, List<(int Quest, Vector3 Position)>>? _questsByActor;

    /// <summary>Empty for an unknown quest or one naming no marker.</summary>
    public IReadOnlyList<PlacedMarker> Markers(int questSno)
    {
        if (_cache.TryGetValue(questSno, out var cached)) return cached;
        var result = new List<PlacedMarker>();
        if (store.Meta(schema.SnoGroup(Quest), questSno) is { } quest)
            foreach (var (markerSet, id) in Handles(quest).Select(h => (h.MarkerSet, h.Id)).Distinct())
                if (markers.Find(markerSet, id) is { } marker) result.Add(marker);
        return _cache[questSno] = result;
    }

    /// <summary>Each phase (QuestPhase.dwUID, in the quest's order) with the markers its scripts name and the actors its callbacks
    /// name. An actor named only by its SNO (live 2026-10-01: QST_Frac_GaleValley_Neyrelle_06 phase 87 names
    /// QST_Frac_GaleValley2_HoradricBook, a gizmo placed by a DRLG tile, with no marker) is any placed copy of it.</summary>
    public IReadOnlyList<QuestPhaseObjects> PhaseObjects(int questSno)
    {
        if (_phaseCache.TryGetValue(questSno, out var cached)) return cached;
        if (store.Meta(schema.SnoGroup(Quest), questSno) is not { } quest) return _phaseCache[questSno] = [];
        var handles = Handles(quest).ToList();
        var actors = ActorTokens(quest).ToList();
        var subQuests = SubQuests(quest, questSno).ToList();
        var worlds = CallbackWorlds(quest).ToList();
        return _phaseCache[questSno] = [.. Phases(quest).Select(phase => PhaseUid(quest, phase)).Select(uid =>
        {
            var shownIn = worlds.Where(w => w.PhaseUid == uid).Select(w => w.World).ToHashSet();
            var named = handles.Where(h => h.PhaseUid == uid).Select(h => (h.MarkerSet, h.Id)).Distinct()
                .Select(h => (Scene: SceneOf(h.MarkerSet), Marker: markers.Find(h.MarkerSet, h.Id)))
                .Where(m => m.Marker is not null).ToList();
            var waitsOn = subQuests.Where(s => s.PhaseUid == uid).Select(s => s.Quest).ToHashSet();
            return new QuestPhaseObjects(uid,
                [.. named.Where(m => m.Scene is null).Select(m => m.Marker!)],
                actors.Where(a => a.PhaseUid == uid).Select(a => a.Sno).ToHashSet(),
                [.. named.Where(m => m.Scene is not null).Select(m => new SceneMarker(m.Scene!.Value, m.Marker!))],
                waitsOn.Count > 0 ? waitsOn : null,
                shownIn.Count > 0 ? shownIn : null);
        })];
    }

    /// <summary>The quests whose scripts name a marker in a layer set of one of <paramref name="frames"/> (world or scene names:
    /// <c>&lt;World&gt; (&lt;Layer&gt;)</c>, <c>&lt;Scene&gt; (&lt;Layer&gt;)</c>); the index over every quest is built on first use.</summary>
    public IReadOnlyList<int> QuestsWithMarkersIn(IEnumerable<string> frames)
    {
        BuildIndexes();
        return [.. frames.SelectMany(f => _questsByFrame!.TryGetValue(f, out var quests) ? quests : []).Distinct()];
    }

    /// <summary>The quests whose scripts name a world-space marker of one of <paramref name="actors"/> within
    /// <paramref name="reach"/> of it. An overworld quest marks its objects in a subzone's layer set
    /// (<c>Scos_Downs (Scos_Downs_FieldsOfRuin)</c>), which no world or scene name matches.</summary>
    public IReadOnlyList<int> QuestsNaming(IEnumerable<(int ActorSno, Vector3 Position)> actors, float reach)
    {
        BuildIndexes();
        return [.. actors
            .SelectMany(a => (_questsByActor!.TryGetValue(a.ActorSno, out var named) ? named : [])
                .Where(n => Vector2.Distance(new(n.Position.X, n.Position.Y), new(a.Position.X, a.Position.Y)) <= reach))
            .Select(n => n.Quest).Distinct()];
    }

    // One pass over every quest builds both indexes; each world-space marker set is read once.
    private void BuildIndexes()
    {
        if (_questsByFrame is not null) return;
        var byFrame = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var handles = new List<(int Quest, int MarkerSet, int Id)>();
        int group = schema.SnoGroup(Quest);
        foreach (var (questSno, _) in store.AllEntries(group))
        {
            if (store.Meta(group, questSno) is not { } quest) continue;
            var named = Handles(quest).Select(h => (questSno, h.MarkerSet, h.Id)).Distinct().ToList();
            foreach (string frame in named.Select(h => FrameOf(store.Name(h.MarkerSet))).OfType<string>().Distinct())
                (byFrame.TryGetValue(frame, out var list) ? list : byFrame[frame] = []).Add(questSno);
            handles.AddRange(named);
        }
        var byActor = new Dictionary<int, List<(int Quest, Vector3 Position)>>();
        int actorGroup = schema.SnoGroup(ActorDefinition);
        foreach (var set in handles.GroupBy(h => h.MarkerSet).Where(set => SceneOf(set.Key) is null))
        {
            var top = markers.TopLevel(set.Key);
            foreach (var (quest, _, id) in set)
                if (top.TryGetValue(id, out var marker) && marker.Group == actorGroup)
                    (byActor.TryGetValue(marker.Sno, out var list) ? list : byActor[marker.Sno] = []).Add((quest, marker.Position));
        }
        (_questsByFrame, _questsByActor) = (byFrame, byActor);
    }

    // "Ancients_Blood_FLEX_POI_CentralChamber_01 (CSD_Frac_Underworld_AncientMausoleum_CentralChamber)": the frame (a world or a
    // scene) comes before the layer.
    private static string? FrameOf(string? markerSetName) =>
        markerSetName?.IndexOf(" (", StringComparison.Ordinal) is > 0 and var cut ? markerSetName[..cut] : null;

    // The scene a marker set belongs to, or null for a world's layer set.
    private int? SceneOf(int markerSet)
    {
        if (FrameOf(store.Name(markerSet)) is not { } frame) return null;
        try { return store.Resolve(schema.SnoGroup(SceneDefinition), frame); }
        catch (ArgumentException) { return null; }
    }

    private IEnumerable<int> Phases(SnoBlob quest) => quest.PolymorphicArray(schema.Offset(Quest, "arQuestPhases"), schema.Size(Phase));

    private uint PhaseUid(SnoBlob quest, int phase) => quest.U32(phase + schema.Offset(Phase, "dwUID"));

    private IEnumerable<int> Callbacks(SnoBlob quest, int phase) =>
        quest.Array(phase + schema.Offset(Phase, "arCallbackSets"), schema.Size(Set))
            .SelectMany(set => quest.Array(set + schema.Offset(Set, "arCallbacks"), schema.Size(Callback)));

    // The world a callback's step is shown in (DT_SNO; none is -1 or 0).
    private IEnumerable<(uint PhaseUid, int World)> CallbackWorlds(SnoBlob quest)
    {
        if (!schema.HasField(Callback, "snoWorld")) yield break;
        int at = schema.Offset(Callback, "snoWorld");
        foreach (int phase in Phases(quest))
        foreach (int callback in Callbacks(quest, phase))
            if (quest.I32(callback + at) is > 0 and var world) yield return (PhaseUid(quest, phase), world);
    }

    // A callback's SNO references are DT_SNO_NAME {group, id}; only actors are kept.
    private IEnumerable<(uint PhaseUid, int Sno)> ActorTokens(SnoBlob quest)
    {
        int actorGroup = schema.SnoGroup(ActorDefinition);
        foreach (int phase in Phases(quest))
        foreach (int callback in Callbacks(quest, phase))
        foreach (int token in quest.Array(callback + schema.Offset(Callback, "arSnonameTokens"), schema.Size(SnoName)))
            if (quest.I32(token) == actorGroup) yield return (PhaseUid(quest, phase), quest.I32(token + sizeof(int)));
    }

    private IEnumerable<(uint PhaseUid, int MarkerSet, int Id)> Handles(SnoBlob quest)
    {
        foreach (var (phaseUid, param, type) in Params(quest))
        {
            if (type != MarkerHandleParam) continue;
            int handle = param + schema.Offset(MarkerHandleParam, "tMarkerHandle");
            yield return (phaseUid, quest.I32(handle + schema.Offset("MarkerHandle", "snoMarkerSet")), quest.I32(handle + schema.Offset("MarkerHandle", "nID")));
        }
    }

    private IEnumerable<(uint PhaseUid, int Quest)> SubQuests(SnoBlob quest, int questSno) =>
        Params(quest).Where(p => p.Type == QuestParam)
            .Select(p => (p.PhaseUid, Quest: quest.I32(p.Param + schema.Offset(QuestParam, "snoQuest"))))
            .Where(s => s.Quest > 0 && s.Quest != questSno);

    // Every script parameter of the callbacks' message maps, the event filters' included, with its phase and its type name.
    private IEnumerable<(uint PhaseUid, int Param, string? Type)> Params(SnoBlob quest)
    {
        int map = schema.Offset(Callback, "tScriptMessageMap");
        foreach (int phase in Phases(quest))
        foreach (int callback in Callbacks(quest, phase))
        {
            var parameterArrays = new List<int> { callback + map + schema.Offset(MessageMap, "unk_492729e") };
            foreach (int filter in quest.Array(callback + map + schema.Offset(MessageMap, "arEventFilters"), schema.Size(Filter)))
                parameterArrays.Add(filter + schema.Offset(Filter, "pMessageFilter"));
            foreach (int array in parameterArrays)
            foreach (int param in quest.PolymorphicElements(array, p => ParamType(quest, p) is { } t ? schema.Size(t) : null) ?? [])
                yield return (PhaseUid(quest, phase), param, ParamType(quest, param));
        }
    }

    // A script parameter names its own type in dwType (the type hash).
    private string? ParamType(SnoBlob quest, int param)
    {
        int at = param + schema.Offset(Param, "dwType");
        return at + sizeof(uint) <= quest.Length ? schema.TypeName(quest.U32(at)) : null;
    }
}
