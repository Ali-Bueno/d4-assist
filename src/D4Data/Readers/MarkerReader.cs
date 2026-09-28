using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Model;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>Reads actor markers from MarkerSetDefinition records, expanding nested prefab marker sets.</summary>
public sealed class MarkerReader(SnoStore store, TypeSchema schema)
{
    private const string MarkerSet = "MarkerSetDefinition";
    private const string Marker = "Marker";

    private int MarkerSetGroup => schema.SnoGroup(MarkerSet);
    private int ActorGroup => schema.SnoGroup("ActorDefinition");

    /// <summary>Appends the actor markers of <paramref name="markerSetSno"/> in scene-local coordinates.</summary>
    public void ReadLayer(int markerSetSno, string layer, Matrix4x4 toScene, int depth, List<MarkerInfo> sink)
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
            var local = LocalMatrix(blob, m) * toScene;
            if (group == ActorGroup)
            {
                var p = local.Translation;
                sink.Add(new MarkerInfo(layer, blob.I32(m + schema.Offset(Marker, "eType")),
                    new SnoRef(sno, store.Name(sno)), [p.X, p.Y, p.Z], null));
            }
            else if (group == MarkerSetGroup && depth < Conventions.MaxPrefabDepth)
            {
                ReadLayer(sno, layer, local, depth + 1, sink);
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
