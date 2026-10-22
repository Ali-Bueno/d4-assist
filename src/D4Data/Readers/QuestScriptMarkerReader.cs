using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A marker in a scene's own layer set (<c>&lt;Scene&gt; (&lt;Layer&gt;)</c>): its position is scene-local, so it lands in the
/// world only where that scene is placed (a DRLG dungeon's tile, live 2026-10-01: the Ancient Mausoleum's central chamber).</summary>
/// <param name="Layer">The layer's marker set: a scene placed twice can carry two layers, one per tile (QST_Skov_Celestia_03's lock
/// rooms A and B, both <c>Cave_Tomb_EW_01</c>, live 2026-10-22). Null for a marker no layer names (an overlay's).</param>
/// <param name="Anchors">The layer's own actor markers, scene-local: one the map marks tells the tile that carries it. Null with no layer.</param>
public sealed record SceneMarker(int SceneSno, PlacedMarker Marker, int? Layer = null, IReadOnlyList<PlacedMarker>? Anchors = null);

/// <summary>A blueprint's own marker set (<c>&lt;Name&gt;_BPMS</c>, no layer) and its actor markers, which anchor it: the instance
/// places the blueprint shifted, not turned (live 2026-10-17: the ship to Skovos by (-32,-127), CSD_Skov_Temis_SmugglersRun_02 by
/// (-128,-128), each read off two actors standing at their markers).</summary>
public sealed record Blueprint(int MarkerSet, IReadOnlyList<PlacedMarker> Anchors);

/// <summary>A marker in a blueprint's marker set: its spot is the blueprint's until the instance's shift is known.</summary>
public sealed record BlueprintMarker(Blueprint Frame, PlacedMarker Marker);

/// <summary>One quest phase's objects: the markers its scripts name in world space and in scenes, and the actor SNOs its callbacks name.</summary>
/// <param name="SubQuests">The quests the phase waits on (ScriptEventParamQuest): the game then shows their phase instead of this one's
/// (live 2026-10-04: S15_QST_Main_03_TriadC_01 phase 81 hands over to the hub's quest S15_Triad_C_Travincal). Null for none.</param>
/// <param name="Worlds">The worlds the phase's callbacks show their step in (QuestCallback.snoWorld): QST_Frac_PaleKnightsKin's
/// phase 152 (find the journals) shows DGN_Frac_LightsWatch, phase 15 (bring them back) Sanctuary_Eastern_Continent. Null for none.</param>
/// <param name="KindMarkers">The markers in a blueprint's marker set, whose spot is the blueprint's, not the world's: the instance
/// moves it (live 2026-10-17, the ship to Skovos: QST_Skov_Prologue_Apostle stood at (6,-58) and Lorath at (16,-46), their markers
/// said (38,69) and (48,81)). They name a kind of object, and a place once the blueprint's shift is known. Null for none.</param>
/// <param name="HiddenMarkers">The markers only callbacks the game doesn't show name (eIndicatorType 3): "In Rathmas Fußstapfen"
/// phase 12, find the untouched forests, shows the trigger at the Rathmic statue and hides two on the way there. Null for none.</param>
public sealed record QuestPhaseObjects(
    uint PhaseUid, IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers,
    IReadOnlySet<int>? SubQuests = null, IReadOnlySet<int>? Worlds = null, IReadOnlyList<BlueprintMarker>? KindMarkers = null,
    IReadOnlySet<PlacedMarker>? HiddenMarkers = null);

/// <summary>The objects a quest's objective scripts name (research/client-memory.md §4, quest gizmos).</summary>
/// <remarks>Walks phases → objective sets → callbacks, keeping every ScriptEventParamMarkerHandle of the script message map's
/// parameters and event filters (resolved through its marker set), every ScriptEventParamQuest there (a sub-quest) and every
/// actor in the callback's <c>arSnonameTokens</c>. Cached per quest; not thread-safe.</remarks>
public sealed class QuestScriptMarkerReader(SnoStore store, TypeSchema schema, MarkerReader markers)
{
    private const string Quest = "QuestDefinition", Phase = "QuestPhase", Set = "QuestObjectiveSet", Callback = "QuestCallback";
    private const string ActorDefinition = "ActorDefinition", SnoName = "DT_SNO_NAME", SceneDefinition = "SceneDefinition";
    private const string WorldDefinition = "WorldDefinition", SubzoneDefinition = "SubzoneDefinition";
    private const string MessageMap = "ScriptMessageMap", Filter = "ScriptMessageMapFilter";
    private const string Param = "ScriptEventParamBase", MarkerHandleParam = "ScriptEventParamMarkerHandle", QuestParam = "ScriptEventParamQuest";
    private const string ActorGroupParam = "ScriptEventParamActorGroup";

    private readonly Dictionary<int, IReadOnlyList<PlacedMarker>> _cache = [];
    private readonly Dictionary<int, IReadOnlyList<QuestPhaseObjects>> _phaseCache = [];
    private Dictionary<string, List<int>>? _questsByFrame;
    private Dictionary<int, List<(int Quest, Vector3 Position)>>? _questsByActor;
    private Dictionary<int, List<int>>? _questsByWorld;
    private readonly Dictionary<int, Blueprint> _blueprints = [];
    private readonly Dictionary<int, IReadOnlyList<PlacedMarker>> _layerAnchors = [];
    private Dictionary<uint, List<(int MarkerSet, PlacedMarker Marker)>>? _groupMarkers;
    private SceneLayerIndex? _sceneLayers;

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
        var groups = ActorGroups(quest).ToList();
        return _phaseCache[questSno] = [.. Phases(quest).Select(phase => PhaseUid(quest, phase)).Select(uid =>
        {
            var shownIn = worlds.Where(w => w.PhaseUid == uid).Select(w => w.World).ToHashSet();
            var named = handles.Where(h => h.PhaseUid == uid).Select(h => (h.MarkerSet, h.Id)).Distinct()
                .Select(h => (h.MarkerSet, Scenes: ScenesOf(h.MarkerSet), Blueprint: BlueprintOf(h.MarkerSet), Marker: markers.Find(h.MarkerSet, h.Id)))
                .Where(m => m.Marker is not null).ToList();
            var waitsOn = subQuests.Where(s => s.PhaseUid == uid).Select(s => s.Quest).ToHashSet();
            var kinds = named.Where(m => m.Blueprint is not null).Select(m => new BlueprintMarker(m.Blueprint!, m.Marker!))
                .Concat(groups.Where(g => g.PhaseUid == uid).SelectMany(g => BlueprintGroupMarkers(g.Group))).Distinct().ToList();
            var overlays = groups.Where(g => g.PhaseUid == uid).SelectMany(g => OverlayGroupMarkers(g.Group).Concat(SceneGroupMarkers(g.Group))).Distinct();
            var hidden = handles.Where(h => h.PhaseUid == uid).GroupBy(h => (h.MarkerSet, h.Id)).Where(h => h.All(n => n.Hidden))
                .Select(h => markers.Find(h.Key.MarkerSet, h.Key.Id)).OfType<PlacedMarker>().ToHashSet();
            return new QuestPhaseObjects(uid,
                [.. named.Where(m => m.Scenes is null && m.Blueprint is null).Select(m => m.Marker!)],
                actors.Where(a => a.PhaseUid == uid).Select(a => a.Sno).ToHashSet(),
                [.. named.Where(m => m.Scenes is not null).SelectMany(m => m.Scenes!.Select(scene => new SceneMarker(scene, m.Marker!, m.MarkerSet, LayerAnchors(m.MarkerSet)))), .. overlays],
                waitsOn.Count > 0 ? waitsOn : null,
                shownIn.Count > 0 ? shownIn : null,
                kinds.Count > 0 ? kinds : null,
                hidden.Count > 0 ? hidden : null);
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

    /// <summary>The one of <paramref name="quests"/> whose scripts name a world-space marker nearest to <paramref name="at"/>, within
    /// <paramref name="reach"/>; null for none. Names the game's generic quest map marker (Symbol_Quest_Proxy), which no quest names
    /// itself, after a quest read as in progress: the nearest quest of all named the fiend's spot after another quest
    /// (live 2026-10-19, "Durch dunkles Glas" instead of "Fangzahn und Flamme").</summary>
    public int? QuestNearest(Vector2 at, float reach, IReadOnlyCollection<int> quests)
    {
        BuildIndexes();
        int? best = null;
        float bestDistance = reach;
        foreach (var named in _questsByActor!.Values)
            foreach (var (quest, position) in named)
            {
                float distance = Vector2.Distance(at, new(position.X, position.Y));
                if (quests.Contains(quest) && distance <= bestDistance) (best, bestDistance) = (quest, distance);
            }
        return best;
    }

    /// <summary>The quests some phase of which shows its step in <paramref name="worldSno"/> (QuestCallback.snoWorld). A world's
    /// own marker set can be named neither after a layer nor after the world (live 2026-10-17: QST_Skov_Prologue_04 marks the
    /// Skovos docks in CSD_Hawe_SailtoSkovos_Docks_BPMS, and its spots move with the generated layout), so no marker finds it.</summary>
    public IReadOnlyList<int> QuestsShownIn(int worldSno)
    {
        BuildIndexes();
        return _questsByWorld!.TryGetValue(worldSno, out var quests) ? quests : [];
    }

    // One pass over every quest builds the indexes; each world-space marker set is read once.
    private void BuildIndexes()
    {
        if (_questsByFrame is not null) return;
        var byFrame = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var byWorld = new Dictionary<int, List<int>>();
        var handles = new List<(int Quest, int MarkerSet, int Id)>();
        int group = schema.SnoGroup(Quest);
        foreach (var (questSno, _) in store.AllEntries(group))
        {
            if (store.Meta(group, questSno) is not { } quest) continue;
            var named = Handles(quest).Select(h => (questSno, h.MarkerSet, h.Id)).Distinct().ToList();
            foreach (string frame in named.Select(h => FrameOf(store.Name(h.MarkerSet))).OfType<string>().Distinct())
                (byFrame.TryGetValue(frame, out var list) ? list : byFrame[frame] = []).Add(questSno);
            foreach (int world in CallbackWorlds(quest).Select(w => w.World).Distinct())
                (byWorld.TryGetValue(world, out var list) ? list : byWorld[world] = []).Add(questSno);
            handles.AddRange(named);
        }
        var byActor = new Dictionary<int, List<(int Quest, Vector3 Position)>>();
        int actorGroup = schema.SnoGroup(ActorDefinition);
        foreach (var set in handles.GroupBy(h => h.MarkerSet).Where(set => ScenesOf(set.Key) is null))
        {
            var top = markers.TopLevel(set.Key);
            foreach (var (quest, _, id) in set)
                if (top.TryGetValue(id, out var marker) && marker.Group == actorGroup)
                    (byActor.TryGetValue(marker.Sno, out var list) ? list : byActor[marker.Sno] = []).Add((quest, marker.Position));
        }
        (_questsByFrame, _questsByActor, _questsByWorld) = (byFrame, byActor, byWorld);
    }

    // "Ancients_Blood_FLEX_POI_CentralChamber_01 (CSD_Frac_Underworld_AncientMausoleum_CentralChamber)": the frame (a world or a
    // scene) comes before the layer.
    private static string? FrameOf(string? markerSetName) =>
        markerSetName?.IndexOf(" (", StringComparison.Ordinal) is > 0 and var cut ? markerSetName[..cut] : null;

    // A blueprint's own marker set: "CSD_Hawe_SailtoSkovos_Docks_BPMS" or one of its sections
    // ("CSD_Skov_Lycander_Mindscape_02_BPMS_Section_01"), no layer (QuestPhaseObjects.KindMarkers); null for another set.
    private Blueprint? BlueprintOf(int markerSet)
    {
        if (_blueprints.TryGetValue(markerSet, out var known)) return known;
        if (store.Name(markerSet) is not { } name || !IsBlueprintSet(name)) return null;
        int actorGroup = schema.SnoGroup(ActorDefinition);
        return _blueprints[markerSet] = new Blueprint(markerSet, [.. markers.TopLevel(markerSet).Values.Where(m => m.Group == actorGroup && m.Sno > 0)]);
    }

    // A scene layer's actor markers (its barrier walls, NPCs, spawners), read once per set.
    private IReadOnlyList<PlacedMarker> LayerAnchors(int markerSet)
    {
        if (_layerAnchors.TryGetValue(markerSet, out var known)) return known;
        int actorGroup = schema.SnoGroup(ActorDefinition);
        return _layerAnchors[markerSet] = [.. markers.TopLevel(markerSet).Values.Where(m => m.Group == actorGroup && m.Sno > 0)];
    }

    // The scenes a marker set belongs to; null for a world's or a subzone's layer set (world space) or a set with no frame. A scene
    // renamed since keeps its sets under the old name, and the scenes whose own layers carry that name place them: the Eldhaime
    // Barracks' secret-door set "TT_PDKeep_CaveRoot_NS_01 (CSD_Scos_EldhaimeRuins_SecretDoor)" lies in
    // TT_Protodun_Keep_to_CaveRoot_NS_01, and read as world space it led back to the entrance (tester, 2026-10-28). Empty for a
    // set of a scene gone from the build (research/dungeon-objectives.md §28).
    private IReadOnlyList<int>? ScenesOf(int markerSet)
    {
        if (FrameOf(store.Name(markerSet)) is not { } frame) return null;
        if (Resolve(SceneDefinition, frame) is { } scene) return [scene];
        if (Resolve(WorldDefinition, frame) is not null || Resolve(SubzoneDefinition, frame) is not null) return null;
        return SceneLayers().ScenesByFrame.GetValueOrDefault(frame) ?? [];
    }

    private int? Resolve(string type, string name)
    {
        try { return store.Resolve(schema.SnoGroup(type), name); }
        catch (ArgumentException) { return null; }
    }

    private static bool IsBlueprintSet(string name) =>
        FrameOf(name) is null && (name.EndsWith("_BPMS", StringComparison.OrdinalIgnoreCase) || name.Contains("_BPMS_", StringComparison.OrdinalIgnoreCase));

    // The blueprint markers of an actor group a step names (ScriptEventParamActorGroup).
    private IEnumerable<BlueprintMarker> BlueprintGroupMarkers(uint group)
    {
        foreach (var (set, marker) in GroupMarkers(group))
            if (BlueprintOf(set) is { } frame) yield return new BlueprintMarker(frame, marker);
    }

    // The markers of an actor group a step names in an overlay: a set no world, scene or blueprint places (no layer, no _BPMS),
    // which the server drops on a dungeon tile's arena footprint, the set's origin there. "Die Erde, die Saat, die Frucht" phase
    // 43 shows a trigger sphere of CSD_Skov_Skartara_Mindscape_03_Holdout at (0,0); the holdout tunnel's footprint sits at its
    // middle (16,16), where the next step's Lilith and memory fragments stand (live 2026-10-21). Every tile with a footprint
    // gets a copy; only placed ones land in the world.
    private IEnumerable<SceneMarker> OverlayGroupMarkers(uint group)
    {
        foreach (var (set, marker) in GroupMarkers(group))
            if (FrameOf(store.Name(set)) is null && BlueprintOf(set) is null)
                foreach (var (scene, footprint) in Footprints())
                    yield return new SceneMarker(scene, marker with { Position = footprint + marker.Position });
    }

    // The markers of an actor group a step names in a scene's layer set, scene-local. "Die Erde, die Saat, die Frucht" phase 52
    // hides a trigger sphere in the traversal bridges' Game layers: at TT_Sightless_Skovos_to_Sightless_Mephisto_NE_Traversal_Bridge_01
    // it stands before the rising bridge the shown sphere lies across, and the guide, its goal behind the bridge, found nothing
    // else to lead to (live 2026-10-21).
    private IEnumerable<SceneMarker> SceneGroupMarkers(uint group)
    {
        foreach (var (set, marker) in GroupMarkers(group))
            foreach (int scene in ScenesOf(set) ?? [])
                yield return new SceneMarker(scene, marker);
    }

    // Every set that holds actor groups (blueprints, overlays and scene layers), read once on first use.
    private List<(int MarkerSet, PlacedMarker Marker)> GroupMarkers(uint group)
    {
        if (_groupMarkers is null)
        {
            _groupMarkers = [];
            foreach (var (set, name) in store.AllEntries(schema.SnoGroup("MarkerSetDefinition")))
            {
                foreach (var (marker, groups) in markers.ActorGroupMarkers(set))
                foreach (uint g in groups.Distinct())
                    (_groupMarkers.TryGetValue(g, out var list) ? list : _groupMarkers[g] = []).Add((set, marker));
            }
        }
        return _groupMarkers.GetValueOrDefault(group) ?? [];
    }

    // The arena footprints (prefab sets Dungeon_Template_*_Footprint_*) in the scenes' layers, scene-local.
    private List<(int Scene, Vector3 Local)> Footprints() => SceneLayers().Footprints;

    // One pass over every scene's layers, on first use: the arena footprints in them, and the scenes that place each frame other
    // than their own name (a tileset's shared base sets, a scene's sets from before it was renamed).
    private SceneLayerIndex SceneLayers()
    {
        if (_sceneLayers is not null) return _sceneLayers;
        var footprints = new List<(int Scene, Vector3 Local)>();
        var byFrame = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        int sceneGroup = schema.SnoGroup(SceneDefinition), markerSetGroup = schema.SnoGroup("MarkerSetDefinition");
        foreach (var (scene, name) in store.AllEntries(sceneGroup))
        {
            if (store.Meta(sceneGroup, scene) is not { } meta) continue;
            foreach (int layer in meta.Array(schema.Offset(SceneDefinition, "arLayers"), schema.Size("LayerData")))
            {
                int set = meta.I32(layer + schema.Offset("LayerData", "snoLayerMarkerset"));
                if (FrameOf(store.Name(set)) is { } frame && !string.Equals(frame, name, StringComparison.OrdinalIgnoreCase))
                {
                    var scenes = byFrame.TryGetValue(frame, out var list) ? list : byFrame[frame] = [];
                    if (!scenes.Contains(scene)) scenes.Add(scene);
                }
                foreach (var marker in markers.TopLevel(set).Values)
                    if (marker.Group == markerSetGroup && IsFootprint(store.Name(marker.Sno))) footprints.Add((scene, marker.Position));
            }
        }
        return _sceneLayers = new(footprints, byFrame.ToDictionary(f => f.Key, f => (IReadOnlyList<int>)f.Value, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record SceneLayerIndex(List<(int Scene, Vector3 Local)> Footprints, IReadOnlyDictionary<string, IReadOnlyList<int>> ScenesByFrame);

    private static bool IsFootprint(string? actor) =>
        actor is not null && actor.StartsWith("Dungeon_Template_", StringComparison.OrdinalIgnoreCase) && actor.Contains("_Footprint", StringComparison.OrdinalIgnoreCase);

    // The actor groups (GBIDs) a phase's scripts name; 0 is none.
    private IEnumerable<(uint PhaseUid, uint Group)> ActorGroups(SnoBlob quest) =>
        Params(quest).Where(p => p.Type == ActorGroupParam)
            .Select(p => (p.PhaseUid, Group: quest.U32(p.Param + schema.Offset(ActorGroupParam, "gbidActorGroup"))))
            .Where(g => g.Group != 0);

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

    // Hidden: named by a callback the game doesn't show as an objective (QuestCallback.eIndicatorType 3, research/client-memory.md §4).
    private IEnumerable<(uint PhaseUid, int MarkerSet, int Id, bool Hidden)> Handles(SnoBlob quest)
    {
        int? indicator = schema.HasField(Callback, "eIndicatorType") ? schema.Offset(Callback, "eIndicatorType") : null;
        foreach (var (phaseUid, callback, param, type) in Params(quest))
        {
            if (type != MarkerHandleParam) continue;
            int handle = param + schema.Offset(MarkerHandleParam, "tMarkerHandle");
            yield return (phaseUid, quest.I32(handle + schema.Offset("MarkerHandle", "snoMarkerSet")), quest.I32(handle + schema.Offset("MarkerHandle", "nID")),
                indicator is { } at && quest.I32(callback + at) == HiddenIndicator);
        }
    }

    private const int HiddenIndicator = 3;

    private IEnumerable<(uint PhaseUid, int Quest)> SubQuests(SnoBlob quest, int questSno) =>
        Params(quest).Where(p => p.Type == QuestParam)
            .Select(p => (p.PhaseUid, Quest: quest.I32(p.Param + schema.Offset(QuestParam, "snoQuest"))))
            .Where(s => s.Quest > 0 && s.Quest != questSno);

    // Every script parameter of the callbacks' message maps, the event filters' included, with its phase and its type name.
    private IEnumerable<(uint PhaseUid, int Callback, int Param, string? Type)> Params(SnoBlob quest)
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
                yield return (PhaseUid(quest, phase), callback, param, ParamType(quest, param));
        }
    }

    // A script parameter names its own type in dwType (the type hash).
    private string? ParamType(SnoBlob quest, int param)
    {
        int at = param + schema.Offset(Param, "dwType");
        return at + sizeof(uint) <= quest.Length ? schema.TypeName(quest.U32(at)) : null;
    }
}
