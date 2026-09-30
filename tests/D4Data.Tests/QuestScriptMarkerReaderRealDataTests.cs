using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class QuestScriptMarkerReaderRealDataTests
{
    private const int DarkenedWayGate = 1232628, DemonBody = 1256947;

    [Theory]
    [InlineData("QST_Frac_Underworld_01", DemonBody, -62.4f, 27.3f)]
    [InlineData("QST_Frac_Underworld_02", DarkenedWayGate, -71.7f, 49.5f)]
    public void DarkenedWayQuests_NameTheGizmoTheirStepUses(string questName, int actorSno, float x, float y)
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        var markers = reader.Markers(store.Resolve(schema.SnoGroup("QuestDefinition"), questName));

        // research/client-memory.md §4: the objects the player used at each quest (live 2026-09-30).
        var marker = Assert.Single(markers, m => m.Sno == actorSno);
        Assert.Equal(x, marker.Position.X, 0.5f);
        Assert.Equal(y, marker.Position.Y, 0.5f);
    }

    [Fact]
    public void TheFirstQuestDoesNotNameTheGate()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        var markers = reader.Markers(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Frac_Underworld_01"));

        Assert.DoesNotContain(markers, m => m.Sno == DarkenedWayGate); // locked while this quest was tracked
    }
}
