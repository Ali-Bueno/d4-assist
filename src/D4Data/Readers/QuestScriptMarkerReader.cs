using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>The placed objects a quest's objective scripts name by marker handle (research/client-memory.md §4, quest gizmos).</summary>
/// <remarks>Walks phases → objective sets → callbacks → the script message map's parameters and event filters, keeping every
/// ScriptEventParamMarkerHandle, resolved through its marker set. Cached per quest; not thread-safe.</remarks>
public sealed class QuestScriptMarkerReader(SnoStore store, TypeSchema schema, MarkerReader markers)
{
    private const string Quest = "QuestDefinition", Phase = "QuestPhase", Set = "QuestObjectiveSet", Callback = "QuestCallback";
    private const string MessageMap = "ScriptMessageMap", Filter = "ScriptMessageMapFilter";
    private const string Param = "ScriptEventParamBase", MarkerHandleParam = "ScriptEventParamMarkerHandle";

    private readonly Dictionary<int, IReadOnlyList<PlacedMarker>> _cache = [];

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

    /// <summary>Each phase's UID (QuestPhase.dwUID), in the quest's order, with the markers its scripts name.</summary>
    public IReadOnlyList<(uint PhaseUid, IReadOnlyList<PlacedMarker> Markers)> PhaseMarkers(int questSno)
    {
        if (store.Meta(schema.SnoGroup(Quest), questSno) is not { } quest) return [];
        var handles = Handles(quest).ToList();
        return [.. PhaseUids(quest).Select(uid => (uid, (IReadOnlyList<PlacedMarker>)[.. handles
            .Where(h => h.PhaseUid == uid).Select(h => (h.MarkerSet, h.Id)).Distinct()
            .Select(h => markers.Find(h.MarkerSet, h.Id)).OfType<PlacedMarker>()]))];
    }

    private IEnumerable<uint> PhaseUids(SnoBlob quest) =>
        quest.PolymorphicArray(schema.Offset(Quest, "arQuestPhases"), schema.Size(Phase)).Select(phase => quest.U32(phase + schema.Offset(Phase, "dwUID")));

    private IEnumerable<(uint PhaseUid, int MarkerSet, int Id)> Handles(SnoBlob quest)
    {
        int map = schema.Offset(Callback, "tScriptMessageMap");
        foreach (int phase in quest.PolymorphicArray(schema.Offset(Quest, "arQuestPhases"), schema.Size(Phase)))
        foreach (int set in quest.Array(phase + schema.Offset(Phase, "arCallbackSets"), schema.Size(Set)))
        foreach (int callback in quest.Array(set + schema.Offset(Set, "arCallbacks"), schema.Size(Callback)))
        {
            var parameterArrays = new List<int> { callback + map + schema.Offset(MessageMap, "unk_492729e") };
            foreach (int filter in quest.Array(callback + map + schema.Offset(MessageMap, "arEventFilters"), schema.Size(Filter)))
                parameterArrays.Add(filter + schema.Offset(Filter, "pMessageFilter"));
            foreach (int array in parameterArrays)
            foreach (int param in quest.PolymorphicElements(array, p => ParamType(quest, p) is { } t ? schema.Size(t) : null) ?? [])
            {
                if (ParamType(quest, param) != MarkerHandleParam) continue;
                int handle = param + schema.Offset(MarkerHandleParam, "tMarkerHandle");
                yield return (quest.U32(phase + schema.Offset(Phase, "dwUID")),
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
