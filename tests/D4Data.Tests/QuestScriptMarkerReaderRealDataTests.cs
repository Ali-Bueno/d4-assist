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
    public void AStepKnowsWhichOfItsMarkersTheGameHides()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // "In Rathmas Fußstapfen" phase 12, find the untouched forests (2026-10-21): three trigger spheres; the game shows the one
        // at the Rathmic statue that phase 21 has the player examine, and hides the two on the way there.
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_X2_Skov_Lycander_06")).Single(p => p.PhaseUid == 12);
        Assert.Equal(3, step.Markers.Count);
        Assert.Equal([2499, 2651], step.HiddenMarkers!.Select(m => (int)MathF.Round(m.Position.X)).Order());
    }

    [Fact]
    public void AStepsOverlayLandsOnTheHoldoutTunnelsFootprint()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // "Die Erde, die Saat, die Frucht" phase 43 (2026-10-21): the shown trigger sphere is in the overlay
        // CSD_Skov_Skartara_Mindscape_03_Holdout, which the holdout tunnel's arena footprint places, at the tile's middle.
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_X2_Skov_Lycander_Mindscape_01")).Single(p => p.PhaseUid == 43);
        var tunnel = store.Resolve(schema.SnoGroup("SceneDefinition"), "Sightless_Skovos_EW_POI_HoldoutTunnel_01");
        var marker = Assert.Single(step.SceneMarkers, m => m.SceneSno == tunnel).Marker;
        Assert.Equal("Trigger_Sphere_8m", store.Name(marker.Sno));
        Assert.Equal(16.3f, marker.Position.X, 0.5f);
        Assert.Equal(15.9f, marker.Position.Y, 0.5f);
    }

    [Fact]
    public void AFramelessSetLandsWhereAPrefabMarkerPlacesIt()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // The Orbei Monastery's dying priest (DOV_Step_OrbeiMonastery_SermonHallWaves phase 13, tester 2026-10-28): his set
        // DOV_Orbei_SermonHallWaves names no frame; a prefab marker in the sermon hall tunnel's layer set places it. Read as
        // world space, the step stood at the world's origin.
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "DOV_Step_OrbeiMonastery_SermonHallWaves")).Single(p => p.PhaseUid == 13);
        var tunnel = store.Resolve(schema.SnoGroup("SceneDefinition"), "Zak_Mona_EW_POI_HoldoutTunnel_01");
        Assert.Empty(step.Markers);
        var marker = Assert.Single(step.SceneMarkers, m => m.SceneSno == tunnel).Marker;
        Assert.Equal("NPC_Step_OrbeiMonastery_MadPriest_M_Praying", store.Name(marker.Sno));
        Assert.Equal(16.0f, marker.Position.X, 0.5f);
        Assert.Equal(14.6f, marker.Position.Y, 0.5f);
    }

    [Fact]
    public void AnActorGroupInAPlacedPrefabLandsThereNotOnEveryFootprint()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // Guulrahn's marketplace vignette (DOV_Step_Guulrahn_SanguineAlley_Vignette_05 phase 40): the cannibals' group lies in a
        // set the marketplace's Game layer places, not one the server drops on arena footprints (372 copies before).
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "DOV_Step_Guulrahn_SanguineAlley_Vignette_05")).Single(p => p.PhaseUid == 40);
        var marketplace = store.Resolve(schema.SnoGroup("SceneDefinition"), "Step_City_POI_FLEX_Marketplace_01");
        Assert.NotEmpty(step.SceneMarkers);
        Assert.All(step.SceneMarkers, m => Assert.Equal(marketplace, m.SceneSno));
    }

    [Fact]
    public void AStepsGroupInASceneLayerLandsInThatScene()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // "Die Erde, die Saat, die Frucht" phase 52 (2026-10-21): the hidden trigger before the rising bridge is in the traversal
        // bridge tile's Game layer.
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_X2_Skov_Lycander_Mindscape_01")).Single(p => p.PhaseUid == 52);
        var bridge = store.Resolve(schema.SnoGroup("SceneDefinition"), "TT_Sightless_Skovos_to_Sightless_Mephisto_NE_Traversal_Bridge_01");
        var marker = Assert.Single(step.SceneMarkers, m => m.SceneSno == bridge).Marker;
        Assert.Equal(23.9f, marker.Position.X, 0.5f);
        Assert.Equal(14.9f, marker.Position.Y, 0.5f);
    }

    [Fact]
    public void ASetOfARenamedSceneLandsInTheSceneWhoseLayersCarryItsName()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // The Eldhaime Barracks (QST_Scos_EldhaimeRuin_02 phase 162, tester 2026-10-28): the trigger past the climb down is in
        // "TT_PDKeep_CaveRoot_NS_01 (CSD_Scos_EldhaimeRuins_SecretDoor)", no scene's name; TT_Protodun_Keep_to_CaveRoot_NS_01's own
        // layers carry it. Read as world space, it led back to the entrance.
        var step = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Scos_EldhaimeRuin_02")).Single(p => p.PhaseUid == 162);
        var tile = store.Resolve(schema.SnoGroup("SceneDefinition"), "TT_Protodun_Keep_to_CaveRoot_NS_01");
        Assert.Empty(step.Markers);
        var marker = Assert.Single(step.SceneMarkers, m => m.SceneSno == tile).Marker;
        Assert.Equal(30f, marker.Position.X, 1f);
        Assert.Equal(17f, marker.Position.Y, 1f);
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
        var (scene, trigger, _, _) = Assert.Single(phase.SceneMarkers);
        Assert.Equal(chamber, store.Name(scene));
        Assert.Equal("Trigger_Sphere_8m", store.Name(trigger.Sno));
        Assert.Equal(-1f, trigger.Position.X, 1f);
        Assert.Equal(51f, trigger.Position.Y, 1f);
    }

    [Fact]
    public void ASceneLayerMarkerKnowsItsLayerAndTheLayersOwnActors()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        var schema = TypeSchema.Load(definitions);
        using var store = new SnoStore(install);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        // Live 2026-10-22, "Die Leere": phase 276 names lock room A's trigger, phase 280 lock room B's, both in Cave_Tomb_EW_01; the
        // layer's barrier wall tells which tile carries it.
        int quest = store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Skov_Celestia_03");
        var phases = reader.PhaseObjects(quest);
        var a = Assert.Single(phases.Single(p => p.PhaseUid == 276).SceneMarkers);
        var b = Assert.Single(phases.Single(p => p.PhaseUid == 280).SceneMarkers);
        Assert.Equal(a.SceneSno, b.SceneSno);
        Assert.Equal("Cave_Tomb_EW_01 (CSD_Skov_Celestia_SpearDungeon_LockRoomA)", store.Name(a.Layer!.Value));
        Assert.Equal("Cave_Tomb_EW_01 (CSD_Skov_Celestia_SpearDungeon_LockRoomB)", store.Name(b.Layer!.Value));
        Assert.Contains(b.Anchors!, m => store.Name(m.Sno) == "DRLGKit_fxKit_harmless_barrierWall_demonic_01");
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

    [Fact]
    public void EachPhaseKnowsTheWorldItsStepIsShownIn()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));

        var phases = reader.PhaseObjects(store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Frac_PaleKnightsKin"));

        // Live 2026-10-17: "Legacies of Light's Watch", the journals found in the dungeon (152), brought back to Margrave (15).
        int world = schema.SnoGroup("WorldDefinition");
        Assert.Equal([store.Resolve(world, "DGN_Frac_LightsWatch")], phases.Single(p => p.PhaseUid == 152).Worlds!);
        Assert.Equal([store.Resolve(world, "Sanctuary_Eastern_Continent")], phases.Single(p => p.PhaseUid == 15).Worlds!);
        Assert.Null(phases.Single(p => p.PhaseUid == 3).Worlds);
    }

    [Fact]
    public void AnOverworldSideQuestIsFoundByTheObjectsItsStepNames()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
        int quest = store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_Scos_Blycroft_01");
        int wardstoneBase = store.Resolve(schema.SnoGroup("ActorDefinition"), "QST_Scos_Blycroft_Wardstone_base");

        // research/client-memory.md §4 (user report 2026-10-02, "Fields of Ruin"): phase 14 places three wardstones on bases its
        // markers name in a subzone layer set, "Scos_Downs (Scos_Downs_FieldsOfRuin)", which no world or scene name finds.
        var phase = reader.PhaseObjects(quest).Single(p => p.PhaseUid == 14);
        Assert.Equal(3, phase.Markers.Count(m => m.Sno == wardstoneBase));
        Assert.Contains(quest, reader.QuestsNaming([(wardstoneBase, new System.Numerics.Vector3(-1767.8f, -487.6f, 0))], reach: 2));
        Assert.DoesNotContain(quest, reader.QuestsNaming([(wardstoneBase, new System.Numerics.Vector3(-1700f, -487.6f, 0))], reach: 2));
    }

    [Fact]
    public void TheSandstormQuestNamesItsSheltersPhaseByPhaseAndTheNextQuestHasMeshifOnThePalaceSteps()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
        int quest = schema.SnoGroup("QuestDefinition"), actor = schema.SnoGroup("ActorDefinition");
        int safeZone = store.Resolve(actor, "BCD_Trigger_EliasPalace_SafeZone"), guide = store.Resolve(actor, "NPC_QST_Kehj_EliasPalace_Guide");

        // research/campaign-routes.md §2: the 2026-10-19 log ran phases 452, 571, 698 in this order.
        var shelters = reader.PhaseObjects(store.Resolve(quest, "QST_Kehj_EliasPalace_02"))
            .SelectMany(p => p.Markers.Where(m => m.Sno == safeZone).Select(m => (p.PhaseUid, m.Position.X, m.Position.Y)))
            .ToList();
        Assert.Equal([452u, 571u, 698u], shelters.Select(s => s.PhaseUid));
        Assert.Equal([-476.6f, -516.1f, -573.2f], shelters.Select(s => MathF.Round(s.X, 1)));

        var meshif = Assert.Single(reader.Markers(store.Resolve(quest, "QST_Kehj_EliasPalace_03")), m => m.Sno == guide);
        Assert.Equal(-672.6f, meshif.Position.X, 0.5f);
        Assert.Equal(7.0f, meshif.Position.Y, 0.5f);
    }

    [Fact]
    public void AStepNamingAnActorGroupLeadsToTheBlueprintMarkersOfThatGroup()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
        int quest = store.Resolve(schema.SnoGroup("QuestDefinition"), "QST_X2_Skov_Philios_Mindscape_02");
        int sphere = store.Resolve(schema.SnoGroup("ActorDefinition"), "Trigger_Sphere_8m");
        int section = store.Resolve(schema.SnoGroup("MarkerSetDefinition"), "CSD_Skov_Lycander_Mindscape_02_BPMS_Section_01");

        // Live 2026-10-18 ("Was gegeben wurde", CSD_Skov_Philios_Mindscape_02): phase 285 asks the player into actor group
        // 1357711822, a Trigger_Sphere_8m by the exit portal in a blueprint section; no marker handle or actor names it, so the
        // dungeon read explored and the guide had nowhere to go.
        var phase = reader.PhaseObjects(quest).Single(p => p.PhaseUid == 285);
        var exit = Assert.Single(phase.KindMarkers!, k => k.Marker.Sno == sphere && k.Marker.Position.Y > 150);
        Assert.Equal(section, exit.Frame.MarkerSet);
        Assert.Equal(-33.4f, exit.Marker.Position.X, 0.1f);
        Assert.Equal(181.0f, exit.Marker.Position.Y, 0.1f);
        Assert.NotEmpty(exit.Frame.Anchors);
    }
}
