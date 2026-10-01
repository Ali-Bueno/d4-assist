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

    [Fact]
    public void EachPhaseNamesItsOwnObjects()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        var phases = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Frac_Underworld_02"));

        // research/client-memory.md §4, quest phases (live 2026-09-30): phase 236 is the blood petal; the entrance
        // blocker that sounded early belongs to phase 213, which comes later in play.
        string Names(uint uid) => string.Join(",", phases.Single(p => p.PhaseUid == uid).Markers.Select(m => store.Name(m.Sno)));
        Assert.Equal("Ancients_Prop_Lilith_BloodPetal", Names(236));
        Assert.Equal("QST_Frac_Underworld_DGN_Entrance_Ancients_Ruins_Blocker_Door", Names(213));
    }

    [Fact]
    public void TheMausoleumsQuestIsFoundByItsWorldAndItsChaseStepNamesATrigger()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
        int quest = store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Frac_Underworld2_02");

        // research/client-memory.md §4 (live 2026-10-01): no quest tracked in the Ancient Mausoleum, phase 80 "chase Lilith". A
        // DRLG dungeon's quest markers sit in its scenes' layer sets, scene-local.
        const string chamber = "Ancients_Blood_FLEX_POI_CentralChamber_01";
        Assert.Contains(quest, reader.QuestsWithMarkersIn([chamber]));
        var phase = reader.PhaseObjects(quest).Single(p => p.PhaseUid == 80);
        Assert.Empty(phase.Markers);
        var (scene, trigger) = Assert.Single(phase.SceneMarkers);
        Assert.Equal(chamber, store.Name(scene));
        Assert.Equal("Trigger_Sphere_8m", store.Name(trigger.Sno));
        Assert.Equal(-1f, trigger.Position.X, 1f);
        Assert.Equal(51f, trigger.Position.Y, 1f);
    }

    [Fact]
    public void APhaseNamesAnActorBySnoAlone()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        var phases = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Frac_GaleValley_Neyrelle_06"));

        // research/client-memory.md §4 (live 2026-10-01, Horadric Vault): phase 87 asks for the Horadric book, a gizmo
        // the DRLG places, so no marker names it; the phase before (277) names only Neyrelle by marker.
        int book = store.Resolve(schema.SnoGroup("ActorDefinition"), "QST_Frac_GaleValley2_HoradricBook");
        var phase = phases.Single(p => p.PhaseUid == 87);
        Assert.Contains(book, phase.ActorSnos);
        Assert.Empty(phase.Markers);
        Assert.DoesNotContain(book, phases.Single(p => p.PhaseUid == 277).ActorSnos);
    }
}
