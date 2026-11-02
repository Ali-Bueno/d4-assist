using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class WhisperCatalogRealDataTests
{
    private const int OpenWorld = 69068;

    private static void WithCatalog(Action<WhisperCatalog, SnoStore> test)
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        test(new WhisperCatalog(store, TypeSchema.Load(definitions)), store);
    }

    [Theory] // The game's event objects, live 2026-11-01 (research/map-points.md §4b)
    [InlineData(323f, -424f, WhisperKind.Legion)]
    [InlineData(2754f, -1399f, WhisperKind.Legion)]
    [InlineData(-498f, 212f, WhisperKind.Legion)]
    [InlineData(1448f, -327f, WhisperKind.Legion)]
    [InlineData(-629f, -1159f, WhisperKind.Legion)]
    [InlineData(2405f, -193f, WhisperKind.Legion)]
    [InlineData(-2072f, -600f, WhisperKind.WorldBoss)]
    public void An_event_objects_spot_is_its_quests(float x, float y, WhisperKind kind) => WithCatalog((catalog, _) =>
    {
        var nearest = catalog.All.Where(w => w.Kind == kind).MinBy(w => MathF.Pow(w.Position.X - x, 2) + MathF.Pow(w.Position.Y - y, 2))!;
        float distance = MathF.Sqrt(MathF.Pow(nearest.Position.X - x, 2) + MathF.Pow(nearest.Position.Y - y, 2));
        Assert.True(distance < 5, $"nearest {nearest.QuestName} at ({nearest.Position.X},{nearest.Position.Y}), {distance} off");
    });

    [Fact]
    public void Each_kind_pays_its_grim_favors_from_the_bounty_definitions() => WithCatalog((catalog, _) =>
    {
        Assert.Equal(1, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Culling_Tier1_Scos_Hills_Woodwraith").Favors);
        Assert.Equal(3, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Harvest_Tier2_Kehj_Caldeum").Favors);
        Assert.Equal(5, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Dungeon_Tier3_Step_Eastern_PallidDelve").Favors);
        Assert.Equal(5, Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss").Favors);
        Assert.Equal(3, Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Kehj_Alcarnus").Favors);
        Assert.DoesNotContain(catalog.All, w => w.Favors == 0);
    });

    [Fact]
    public void World_bosses_and_legions_list_at_every_place_they_can_rise() => WithCatalog((catalog, _) =>
    {
        Assert.Equal(WhisperKind.WorldBoss, Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss").Kind);
        Assert.Equal(WhisperKind.Legion, Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Kehj_Alcarnus").Kind);
        Assert.True(catalog.All.Count(w => w.Kind == WhisperKind.WorldBoss) >= 5);
        Assert.True(catalog.All.Count(w => w.Kind == WhisperKind.Legion) >= 5);
    });

    [Fact]
    public void PvP_seasons_and_breadcrumbs_are_left_out_and_every_whisper_has_a_spot() => WithCatalog((catalog, _) =>
    {
        Assert.True(catalog.All.Count > 500);
        Assert.DoesNotContain(catalog.All, w => w.QuestName.Contains("pvp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.Contains("Breadcrumb", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.StartsWith("S07_", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.StartsWith("Bounty_Helltide_", StringComparison.Ordinal));
        Assert.All(catalog.All, w => Assert.Equal(OpenWorld, w.WorldSno));
        Assert.Equal(catalog.All.Count, catalog.All.Select(w => w.QuestSno).Distinct().Count());
    });

    [Fact]
    public void A_whisper_is_up_while_its_world_state_holds_its_value() => WithCatalog((catalog, _) =>
    {
        var whisper = catalog.All.First(w => w.QuestName.StartsWith("Bounty_OWC_", StringComparison.Ordinal));
        Assert.True(whisper.IsUp(new Dictionary<int, int> { [whisper.WorldStateSno] = whisper.WorldStateValue }));
        Assert.False(whisper.IsUp(new Dictionary<int, int> { [whisper.WorldStateSno] = whisper.WorldStateValue + 1 }));
        Assert.False(whisper.IsUp(new Dictionary<int, int>()));
    });

    // Live 2026-11-01, an hour before the boss of 16:00: Dry Steppes and Skovos, the map's table held only the Dry Steppes.
    [Fact]
    public void A_world_boss_lists_by_its_pin_and_with_one_held_by_its_regions_state() => WithCatalog((catalog, store) =>
    {
        var step = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Step_Boss");
        var skov = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Skov_Boss");
        var hawe = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss");
        Assert.Equal("WE_Sanc_Skov", store.Name(skov.RegionEventStateSno));
        var states = new[] { step, skov, hawe }.ToDictionary(w => w.WorldStateSno, w => w.WorldStateValue);
        states[step.RegionEventStateSno] = 3;
        states[skov.RegionEventStateSno] = 3;
        IReadOnlyDictionary<int, int> read = states;

        Assert.DoesNotContain(catalog.Listed(read, new HashSet<int>()), w => w.Kind == WhisperKind.WorldBoss); // the last boss's state, no pin
        var listed = catalog.Listed(read, new HashSet<int> { step.QuestSno }).Where(w => w.Kind == WhisperKind.WorldBoss).Select(w => w.QuestName);
        Assert.Equal(["Bounty_WE_Tier3_Skov_Boss", "Bounty_WE_Tier3_Step_Boss"], listed.Order());
    });

    [Fact]
    public void A_world_boss_without_an_event_of_its_own_starts_with_the_other_and_a_legion_does_not() => WithCatalog((catalog, _) =>
    {
        var scos = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Scos_Boss");
        var skov = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Skov_Boss");
        var legion = Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Step_SteppesOverrun");
        int? OfEvent(Whisper w) => w.QuestSno == scos.QuestSno ? 38 : null;

        Assert.Equal(38, catalog.MinutesToStart(scos, OfEvent));
        Assert.Equal(38, catalog.MinutesToStart(skov, OfEvent));
        Assert.Null(catalog.MinutesToStart(legion, OfEvent));
        Assert.Null(catalog.MinutesToStart(skov, _ => null));
    });

    [Fact]
    public void A_legion_is_up_by_its_event_alone_and_a_world_boss_also_by_its_pin() => WithCatalog((catalog, _) =>
    {
        var coming = Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Step_SteppesOverrun");
        var past = Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Naha_Chakhir");
        var boss = Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Scos_Boss");

        var up = catalog.EventsUp(new HashSet<int> { past.QuestSno, boss.QuestSno }, w => w.QuestSno == coming.QuestSno);

        Assert.Equal(new HashSet<int> { coming.QuestSno, boss.QuestSno }, up);
    });

    [Fact]
    public void A_legion_lists_only_by_its_pin_and_a_plain_whisper_by_its_state() => WithCatalog((catalog, _) =>
    {
        var legion = Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Kehj_Alcarnus");
        var plain = catalog.All.First(w => w.Kind == WhisperKind.Whisper);
        var states = new Dictionary<int, int> { [legion.WorldStateSno] = legion.WorldStateValue, [plain.WorldStateSno] = plain.WorldStateValue };

        Assert.DoesNotContain(catalog.Listed(states, new HashSet<int>()), w => w.QuestSno == legion.QuestSno);
        Assert.Contains(catalog.Listed(states, new HashSet<int>()), w => w.QuestSno == plain.QuestSno);
        Assert.Contains(catalog.Listed(states, new HashSet<int> { legion.QuestSno }), w => w.QuestSno == legion.QuestSno);
    });

    [Fact]
    public void Whispers_have_the_games_names() => WithCatalog((catalog, store) =>
    {
        if (store.Text("deDE", "Quest_Bounty_Harvest_Tier2_Kehj_Caldeum", "Name") is null) return; // no German text
        Assert.Equal("Ernte: Caldeum", catalog.Name(Assert.Single(catalog.All, w => w.QuestName == "Bounty_Harvest_Tier2_Kehj_Caldeum"), "deDE"));
        Assert.Equal("Weltboss", catalog.Name(Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss"), "deDE"));
    });
}
