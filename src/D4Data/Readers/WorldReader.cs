using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Model;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A scene placement read from WorldServerData.ptSceneChunks.</summary>
public sealed record SceneChunkInfo(int SceneSno, Vector3 Position, Quaternion Rotation, int LevelAreaSno);

/// <summary>Reads WorldDefinition records (group World).</summary>
public sealed class WorldReader(SnoStore store, TypeSchema schema)
{
    private const string World = "WorldDefinition";
    private const string ServerData = "WorldServerData";
    private const string Chunk = "SceneChunk";

    public int Group => schema.SnoGroup(World);

    public (WorldInfo Info, IReadOnlyList<SceneChunkInfo> Chunks) Read(int worldSno)
    {
        var meta = store.Meta(Group, worldSno) ?? throw new InvalidDataException($"World {worldSno} has no Meta.");
        int layout = meta.I32(schema.Offset(World, "eLayoutType"));
        var servers = new List<ServerDataInfo>();
        var chunks = new List<SceneChunkInfo>();

        foreach (var sd in meta.Array(schema.Offset(World, "ptServerData"), schema.Size(ServerData)))
        {
            var sdChunks = meta.Array(sd + schema.Offset(ServerData, "ptSceneChunks"), schema.Size(Chunk))
                .Select(c => ReadChunk(meta, c)).ToList();
            chunks.AddRange(sdChunks);
            var min = new[] { meta.F32(sd + schema.Offset(ServerData, "flMinX")), meta.F32(sd + schema.Offset(ServerData, "flMinY")) };
            var max = new[] { meta.F32(sd + schema.Offset(ServerData, "flMaxX")), meta.F32(sd + schema.Offset(ServerData, "flMaxY")) };
            servers.Add(new ServerDataInfo(meta.I32(sd + schema.Offset(ServerData, "nSeed")), sdChunks.Count, new Box(min, max)));
        }

        var info = new WorldInfo(
            Ref(worldSno),
            layout,
            Conventions.LayoutKind(layout),
            meta.I32(schema.Offset(World, "bRandomDungeonWorld")) != 0,
            meta.F32(schema.Offset(World, "flGridSize")),
            OptionalRef(meta.I32(schema.Offset(World, "snoSubzoneDefault"))),
            SnoList(meta, schema.Offset(World, "arDRLGFloors")),
            SnoList(meta, schema.Offset(World, "arDRLGLevelAreas")),
            servers);
        return (info, chunks);
    }

    private SceneChunkInfo ReadChunk(SnoBlob meta, int chunk)
    {
        int transform = chunk + schema.Offset(Chunk, "transform");
        int spec = chunk + schema.Offset(Chunk, "tSceneSpec");
        return new SceneChunkInfo(
            meta.I32(chunk + schema.Offset(Chunk, "snoname")),
            meta.Vec3(transform + schema.Offset("PRTransform", "wp")),
            meta.Quat(transform + schema.Offset("PRTransform", "q")),
            meta.I32(spec + schema.Offset("SceneSpecification", "snoLevelArea")));
    }

    private List<SnoRef> SnoList(SnoBlob meta, int field) =>
        meta.Array(field, schema.Size("DT_SNO")).Select(p => Ref(meta.I32(p))).ToList();

    private SnoRef Ref(int sno) => new(sno, store.Name(sno));

    private SnoRef? OptionalRef(int sno) => sno > 0 ? Ref(sno) : null;
}
