using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Model;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A marker naming any SNO (group + id), positioned in its marker set's frame.</summary>
public sealed record PlacedMarker(int Group, int Sno, Vector3 Position);

/// <summary>Reads actor markers from MarkerSetDefinition records, expanding nested prefab marker sets.</summary>
public sealed class MarkerReader(SnoStore store, TypeSchema schema)
{
    private const string MarkerSet = "MarkerSetDefinition";
    private const string Marker = "Marker";

    private int MarkerSetGroup => schema.SnoGroup(MarkerSet);
    private int ActorGroup => schema.SnoGroup("ActorDefinition");

    /// <summary>Appends the actor markers of <paramref name="markerSetSno"/> in scene-local coordinates.</summary>
    public void ReadLayer(int markerSetSno, string layer, Matrix4x4 toScene, int depth, List<MarkerInfo> sink) =>
        Walk(markerSetSno, toScene, depth, (group, sno, type, local, prefab) =>
        {
            if (group != ActorGroup) return;
            var p = local.Translation;
            sink.Add(new MarkerInfo(layer, type, new SnoRef(sno, store.Name(sno)), [p.X, p.Y, p.Z], null, prefab));
        });

    /// <summary>Every marker of <paramref name="markerSetSno"/> and its nested prefabs, whatever SNO group it names
    /// (actors, quests, ...), in the set's own frame: world space for a subzone's marker set.</summary>
    public IReadOnlyList<PlacedMarker> ReadAll(int markerSetSno)
    {
        var sink = new List<PlacedMarker>();
        Walk(markerSetSno, Matrix4x4.Identity, 0, (group, sno, _, local, _) => sink.Add(new PlacedMarker(group, sno, local.Translation)));
        return sink;
    }

    /// <summary>The top-level marker <paramref name="markerId"/> (Marker.nID) of a marker set, in the set's frame; null when absent.</summary>
    public PlacedMarker? Find(int markerSetSno, int markerId) => TopLevel(markerSetSno).GetValueOrDefault(markerId);

    /// <summary>The top-level markers of a marker set by Marker.nID, in the set's frame; empty when the set is unreadable.</summary>
    public IReadOnlyDictionary<int, PlacedMarker> TopLevel(int markerSetSno)
    {
        var result = new Dictionary<int, PlacedMarker>();
        if (store.Meta(MarkerSetGroup, markerSetSno) is not { } meta) return result;
        var blob = schema.IsInPayload(MarkerSet, "tMarkerSet") ? store.Payload(MarkerSetGroup, markerSetSno) : meta;
        if (blob is null) return result;
        int nameField = schema.Offset(Marker, "snoname");
        foreach (var m in blob.Array(schema.Offset(MarkerSet, "tMarkerSet"), schema.Size(Marker)))
            result.TryAdd(blob.I32(m + schema.Offset(Marker, "nID")),
                new PlacedMarker(blob.I32(m + nameField), blob.I32(m + nameField + sizeof(int)), LocalMatrix(blob, m).Translation));
        return result;
    }

    /// <summary>The marker sets a marker set places as prefabs at its top level, each with its transform into the set's frame.</summary>
    public IReadOnlyList<(int MarkerSet, Matrix4x4 ToFrame)> Prefabs(int markerSetSno)
    {
        var result = new List<(int, Matrix4x4)>();
        if (store.Meta(MarkerSetGroup, markerSetSno) is not { } meta) return result;
        var blob = schema.IsInPayload(MarkerSet, "tMarkerSet") ? store.Payload(MarkerSetGroup, markerSetSno) : meta;
        if (blob is null) return result;
        int nameField = schema.Offset(Marker, "snoname");
        foreach (var m in blob.Array(schema.Offset(MarkerSet, "tMarkerSet"), schema.Size(Marker)))
            if (blob.I32(m + nameField) == MarkerSetGroup && blob.I32(m + nameField + sizeof(int)) is > 0 and var prefab)
                result.Add((prefab, LocalMatrix(blob, m)));
        return result;
    }

    /// <summary>The top-level markers of a marker set that belong to actor groups, with those groups' GBIDs, in the set's frame:
    /// Marker.ptBase holds a MarkerActorData whose ptServerData lists arActorGroups. A quest step names such a group instead of
    /// an actor (live 2026-10-18, QST_X2_Skov_Philios_Mindscape_02 phase 285: "enter" a Trigger_Sphere_8m of group 1357711822).</summary>
    public IReadOnlyList<(PlacedMarker Marker, IReadOnlyList<uint> Groups)> ActorGroupMarkers(int markerSetSno)
    {
        var result = new List<(PlacedMarker, IReadOnlyList<uint>)>();
        if (store.Meta(MarkerSetGroup, markerSetSno) is not { } meta) return result;
        var blob = schema.IsInPayload(MarkerSet, "tMarkerSet") ? store.Payload(MarkerSetGroup, markerSetSno) : meta;
        if (blob is null || !schema.HasType(ActorData)) return result;
        int nameField = schema.Offset(Marker, "snoname"), typeField = schema.Offset("MarkerDataBase", "dwType");
        foreach (var m in blob.Array(schema.Offset(MarkerSet, "tMarkerSet"), schema.Size(Marker)))
        {
            var groups = new List<uint>();
            int? sizeOf(int p) => p + typeField + sizeof(uint) <= blob.Length && schema.TypeName(blob.U32(p + typeField)) is { } t ? schema.Size(t) : null;
            foreach (int data in blob.PolymorphicElements(m + schema.Offset(Marker, "ptBase"), sizeOf) ?? [])
            {
                if (schema.TypeName(blob.U32(data + typeField)) != ActorData) continue;
                foreach (int server in blob.Array(data + schema.Offset(ActorData, "ptServerData"), schema.Size("MarkerServerData")))
                foreach (int gbid in blob.Array(server + schema.Offset("MarkerServerData", "arActorGroups"), sizeof(uint)))
                    groups.Add(blob.U32(gbid));
            }
            if (groups.Count > 0)
                result.Add((new PlacedMarker(blob.I32(m + nameField), blob.I32(m + nameField + sizeof(int)), LocalMatrix(blob, m).Translation), groups));
        }
        return result;
    }

    private const string ActorData = "MarkerActorData";

    // Visits every non-prefab marker with its composed transform and the innermost prefab that placed it (null at the top),
    // expanding nested prefab marker sets.
    private void Walk(int markerSetSno, Matrix4x4 parent, int depth, Action<int, int, int, Matrix4x4, PrefabPlacement?> visit,
        PrefabPlacement? prefab = null)
    {
        var meta = store.Meta(MarkerSetGroup, markerSetSno);
        if (meta is null) return;
        var blob = schema.IsInPayload(MarkerSet, "tMarkerSet") ? store.Payload(MarkerSetGroup, markerSetSno) : meta;
        if (blob is null) return;

        int nameField = schema.Offset(Marker, "snoname");
        foreach (var m in blob.Array(schema.Offset(MarkerSet, "tMarkerSet"), schema.Size(Marker)))
        {
            // DT_SNO_NAME = { i32 group; i32 id } (d4data parse.js).
            int group = blob.I32(m + nameField), sno = blob.I32(m + nameField + sizeof(int));
            if (sno <= 0) continue;
            var local = LocalMatrix(blob, m) * parent;
            if (group == MarkerSetGroup)
            {
                if (depth < Conventions.MaxPrefabDepth) Walk(sno, local, depth + 1, visit, new PrefabPlacement(sno, local.Translation));
            }
            else
            {
                visit(group, sno, blob.I32(m + schema.Offset(Marker, "eType")), local, prefab);
            }
        }
    }

    private Matrix4x4 LocalMatrix(SnoBlob blob, int marker)
    {
        int transform = marker + schema.Offset(Marker, "transform");
        var position = blob.Vec3(transform + schema.Offset("PRTransform", "wp"));
        var rotation = blob.Quat(transform + schema.Offset("PRTransform", "q"));
        var scale = blob.Vec3(marker + schema.Offset(Marker, "vecScale"));
        if (scale == Vector3.Zero) scale = Vector3.One;
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }
}
