using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class CellBytesDecoderRealDataTests
{
    private const string Scene = "SceneDefinition", PathData = "AIPathData", Cell = "AICellData", Grid = "Type_5f1859b4";
    private const int WordsPerLayer = 256, CoreStart = WordsPerLayer, CoreEnd = 17 * WordsPerLayer; // levels 4-67

    [Theory]
    [InlineData("ProtoDun_Keep_NSEW_01")]
    [InlineData("Triad_B_ThroneRoom_N02_E03_DGN")]
    public void DrlgTile_DecodedCellBytesMatchTheShippedDetailGrids(string tile)
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        int group = schema.SnoGroup(Scene), sno = store.Resolve(group, tile);
        var meta = store.Meta(group, sno)!;
        var blob = store.Payload(group, sno) ?? meta;
        int data = meta.ArrayDescriptor(schema.Offset(Scene, "ptAIPathData")).Offset;

        var details = blob.Array(data + schema.Offset(PathData, "unk_5d4c076"), schema.Size(Grid))
            .ToDictionary(g => blob.U64(g + schema.Offset(Grid, "m_cellId")));
        var blobs = blob.Array(data + schema.Offset(PathData, "m_cellBytes"), schema.Size("AICellBytes")).ToList();
        int compared = 0;
        foreach (var c in blob.Array(data + schema.Offset(PathData, "m_cellData"), schema.Size(Cell)))
        {
            if (!details.TryGetValue(blob.U64(c + schema.Offset(Cell, "m_cellId")), out int detail)) continue;
            var bytes = blob.ByteArray(blobs[blob.I32(c + schema.Offset(Cell, "unk_c225718"))] + schema.Offset("AICellBytes", "m_data"));
            var geometry = CellBytesDecoder.Geometry(bytes);
            var surfaces = CellBytesDecoder.Surfaces(bytes);
            int all = detail + schema.Offset(Grid, "m_detailGrid"), walkable = detail + schema.Offset(Grid, "m_detailGridWalkable");
            for (int w = CoreStart; w < CoreEnd; w++)
            {
                Assert.Equal(blob.U64(all + w * sizeof(ulong)), geometry[w]);
                Assert.Equal(0ul, surfaces[w] & ~blob.U64(walkable + w * sizeof(ulong))); // surfaces are walkable voxels
            }
            compared++;
        }
        Assert.True(compared > 0);
    }

    [Fact]
    public void StaticScene_GetsAWalkableMapFromItsCellBytes()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);

        // The Darkened Way (Frac_Underworld), a static world without shipped detail grids.
        var walkability = reader.Read(store.Resolve(reader.Group, "Frac_Underworld_N01_E01")).Walkability;

        Assert.NotNull(walkability?.Map);
        Assert.InRange(walkability!.WalkableArea ?? 0, 500f, 2000f);
    }
}
