using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A marker in a scene's own layer set (<c>&lt;Scene&gt; (&lt;Layer&gt;)</c>): its position is scene-local, so it lands in the
/// world only where that scene is placed (a DRLG dungeon's tile, live 2026-10-01: the Ancient Mausoleum's central chamber).</summary>
public sealed record SceneMarker(int SceneSno, PlacedMarker Marker);

/// <summary>One quest phase's objects: the markers its scripts name in world space and in scenes, and the actor SNOs its callbacks name.</summary>
public sealed record QuestPhaseObjects(
    uint PhaseUid, IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers);

/// <summary>The objects a quest's objective scripts name (research/client-memory.md §4, quest gizmos).</summary>
/// <remarks>Walks phases → objective sets → callbacks, keeping every ScriptEventParamMarkerHandle of the script message map's
/// parameters and event filters (resolved through its marker set) and every actor in the callback's <c>arSnonameTokens</c>.
/// Cached per quest; not thread-safe.</remarks>
public sealed class QuestScriptMarkerReader(SnoStore store, TypeSchema schema, MarkerReader markers)
{
    private const string Quest = "QuestDefinition", Phase = "QuestPhase", Set = "QuestObjectiveSet", Callback = "QuestCallback";
    private const string ActorDefinition = "ActorDefinition", SnoName = "DT_SNO_NAME", SceneDefinition = "SceneDefinition";
    private const string MessageMap = "ScriptMessageMap", Filter = "ScriptMessageMapFilter";
    private const string Param = "ScriptEventParamBase", MarkerHandleParam = "ScriptEventParamMarkerHandle";

    private readonly Dictionary<int, IReadOnlyList<PlacedMarker>> _cache = [];
    private Dictionary<string, List<int>>? _questsByFrame;

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
        if (store.Meta(schema.SnoGroup(Quest), questSno) is not { } quest) return [];
        var handles = Handles(quest).ToList();
        var actors = ActorTokens(quest).ToList();
        return [.. Phases(quest).Select(phase => PhaseUid(quest, phase)).Select(uid =>
        {
            var named = handles.Where(h => h.PhaseUid == uid).Select(h => (h.MarkerSet, h.Id)).Distinct()
                .Select(h => (Scene: SceneOf(h.MarkerSet), Marker: markers.Find(h.MarkerSet, h.Id)))
                .Where(m => m.Marker is not null).ToList();
            return new QuestPhaseObjects(uid,
                [.. named.Where(m => m.Scene is null).Select(m => m.Marker!)],
                actors.Where(a => a.PhaseUid == uid).Select(a => a.Sno).ToHashSet(),
                [.. named.Where(m => m.Scene is not null).Select(m => new SceneMarker(m.Scene!.Value, m.Marker!))]);
        })];
    }

    /// <summary>The quests whose scripts name a marker in a layer set of one of <paramref name="frames"/> (world or scene names:
    /// <c>&lt;World&gt; (&lt;Layer&gt;)</c>, <c>&lt;Scene&gt; (&lt;Layer&gt;)</c>); the index over every quest is built on first use.</summary>
    public IReadOnlyList<int> QuestsWithMarkersIn(IEnumerable<string> frames)
    {
        if (_questsByFrame is null)
        {
            _questsByFrame = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            int group = schema.SnoGroup(Quest);
            foreach (var (questSno, _) in store.AllEntries(group))
            {
                if (store.Meta(group, questSno) is not { } quest) continue;
                foreach (string frame in Handles(quest).Select(h => FrameOf(store.Name(h.MarkerSet))).OfType<string>().Distinct())
                    (_questsByFrame.TryGetValue(frame, out var list) ? list : _questsByFrame[frame] = []).Add(questSno);
            }
        }
        return [.. frames.SelectMany(f => _questsByFrame.TryGetValue(f, out var quests) ? quests : []).Distinct()];
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
        int map = schema.Offset(Callback, "tScriptMessageMap");
        foreach (int phase in Phases(quest))
        foreach (int callback in Callbacks(quest, phase))
        {
            var parameterArrays = new List<int> { callback + map + schema.Offset(MessageMap, "unk_492729e") };
            foreach (int filter in quest.Array(callback + map + schema.Offset(MessageMap, "arEventFilters"), schema.Size(Filter)))
                parameterArrays.Add(filter + schema.Offset(Filter, "pMessageFilter"));
            foreach (int array in parameterArrays)
            foreach (int param in quest.PolymorphicElements(array, p => ParamType(quest, p) is { } t ? schema.Size(t) : null) ?? [])
            {
                if (ParamType(quest, param) != MarkerHandleParam) continue;
                int handle = param + schema.Offset(MarkerHandleParam, "tMarkerHandle");
                yield return (PhaseUid(quest, phase),
                    quest.I32(handle + schema.Offset("MarkerHandle", "snoMarkerSet")), quest.I32(handle + schema.Offset("MarkerHandle", "nID")));
            }
        }
    }

    // A script parameter names its own type in dwType (the type hash).
    private string? ParamType(SnoBlob quest, int param)
    {
        int at = param + schema.Offset(Param, "dwType");
        return at + sizeof(uint) <= quest.Length ? schema.TypeName(quest.U32(at)) : null;
    }
}
