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
        Walk(markerSetSno, toScene, depth, (group, sno, type, local) =>
        {
            if (group != ActorGroup) return;
            var p = local.Translation;
            sink.Add(new MarkerInfo(layer, type, new SnoRef(sno, store.Name(sno)), [p.X, p.Y, p.Z], null));
        });

    /// <summary>Every marker of <paramref name="markerSetSno"/> and its nested prefabs, whatever SNO group it names
    /// (actors, quests, ...), in the set's own frame: world space for a subzone's marker set.</summary>
    public IReadOnlyList<PlacedMarker> ReadAll(int markerSetSno)
    {
        var sink = new List<PlacedMarker>();
        Walk(markerSetSno, Matrix4x4.Identity, 0, (group, sno, _, local) => sink.Add(new PlacedMarker(group, sno, local.Translation)));
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

    // Visits every non-prefab marker with its composed transform, expanding nested prefab marker sets.
    private void Walk(int markerSetSno, Matrix4x4 parent, int depth, Action<int, int, int, Matrix4x4> visit)
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
                if (depth < Conventions.MaxPrefabDepth) Walk(sno, local, depth + 1, visit);
            }
            else
            {
                visit(group, sno, blob.I32(m + schema.Offset(Marker, "eType")), local);
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
