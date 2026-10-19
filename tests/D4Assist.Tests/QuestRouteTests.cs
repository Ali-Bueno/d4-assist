using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Speech;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class QuestRouteTests
{
    // The sandstorm crossing as the game data places it (MarkerSet 577564, research/campaign-routes.md).
    private static readonly Vector2 Start = new(-491.6f, 45.1f);
    private static readonly Vector2 A = new(-573.2f, 40.2f), B = new(-476.6f, -31.7f), C = new(-516.1f, -142.7f);
    private static readonly Vector2 PalaceSteps = new(-672.6f, 7.0f); // QST_Kehj_EliasPalace_03's Meshif
    private const string World = "Sanctuary_Eastern_Continent";
    private const int GuideActor = 576066, OtherActor = 12;

    // ---- world classification ----

    [Fact]
    public void The_open_world_is_overworld_and_other_story_areas_are_radar_only_when_reads_outside_dungeons_are_on()
    {
        Assert.Equal(WorldKind.Overworld, WorldClassifier.KindOf(1, World, null, outsideDungeons: true));
        Assert.Equal(WorldKind.RadarOnly, WorldClassifier.KindOf(1, "Frac_Underworld", null, outsideDungeons: true));
        Assert.Equal(WorldKind.RadarOnly, WorldClassifier.KindOf(0, "Hellgate", null, outsideDungeons: true));
    }

    [Theory]
    [InlineData("Limbo")]
    [InlineData("FrontendStartup")]
    [InlineData("FrontEnd_CharacterCreate_X2")]
    [InlineData("Character_Select_Druid_World")]
    [InlineData("PaperDoll_Rogue")]
    [InlineData("CL_SoloLoadingScreen_World")]
    [InlineData("IGC_TWB_Cine_World")]
    public void Menu_loading_and_cinematic_worlds_stay_other(string name)
    {
        Assert.Equal(WorldKind.Other, WorldClassifier.KindOf(1, name, null, outsideDungeons: true));
    }

    [Fact]
    public void Dungeons_and_radar_only_worlds_keep_their_kind_and_the_switch_turns_the_overworld_off()
    {
        Assert.Equal(WorldKind.Dungeon, WorldClassifier.KindOf(Conventions.LayoutDrlg, "DGN_Kehj_HakansRefuge", null, outsideDungeons: true));
        Assert.Equal(WorldKind.RadarOnly, WorldClassifier.KindOf(1, "Mercenary_Hideout", ["Mercenary_Hideout"], outsideDungeons: true));
        Assert.Equal(WorldKind.Other, WorldClassifier.KindOf(1, World, null, outsideDungeons: false));
        Assert.Equal(WorldKind.Other, WorldClassifier.KindOf(1, "Frac_Underworld", null, outsideDungeons: false));
    }

    [Fact]
    public void Navigation_stays_silent_in_the_overworld()
    {
        var speech = new FakeSpeech();
        var feed = new FakeFeed { CurrentWorld = (World, false), CurrentWorldKind = WorldKind.Overworld };
        _ = new NavigationBridge(feed, speech, announcementsEnabled: true, diagnostics: false);

        // The radar-only world test's walk, which speaks in an ordinary dungeon (RadarOnlyWorldTests).
        feed.RaiseWorld(World);
        feed.RaiseTiles(new TileState(10, "a", 0, 0, 96, 96, 0, 1, 1), new TileState(11, "b", 96, 0, 96, 96, 0, 2, 2));
        for (int i = 0; i < 5; i++) feed.RaisePlayer(new Vector2(10 + i * 10, 10));

        Assert.Empty(speech.Spoken);
    }

    // ---- order and guide ----

    [Fact]
    public void The_shelters_are_visited_in_the_shortest_walk_from_the_quest_marker()
    {
        // Listed out of order; nearest-first from the start would pick B (78 units) before A (82).
        Assert.Equal([A, B, C], RouteOrder.FromStart(Start, [C, B, A]));
    }

    [Fact]
    public void Ending_on_the_palace_steps_orders_the_shelters_as_the_quest_phases_name_them()
    {
        // Phases 452, 571 and 698 of QST_Kehj_EliasPalace_02 name B, C, A; the 2026-10-19 log ran them in that order.
        Assert.Equal([B, C, A], RouteOrder.FromStart(Start, [A, B, C], PalaceSteps));
    }

    [Fact]
    public void Joining_at_the_start_or_between_points_targets_the_end_of_the_nearest_leg()
    {
        var guide = new RouteGuide(Start, [A, B, C], QuestRouteBridge.ReachRadius);

        guide.JoinAt(Start + new Vector2(0, -5)); // nearer to B than to A, but on the first leg
        Assert.Equal(A, guide.Target);

        guide.JoinAt(Vector2.Lerp(A, B, 0.5f));
        Assert.Equal(B, guide.Target);

        guide.JoinAt(B); // standing on a point: the next leg
        Assert.Equal(C, guide.Target);
    }

    [Fact]
    public void Reaching_a_later_point_passes_the_earlier_ones()
    {
        var guide = new RouteGuide(Start, [A, B, C], QuestRouteBridge.ReachRadius);

        Assert.False(guide.Update(Start));
        Assert.True(guide.Update(B + new Vector2(2, 2)));
        Assert.Equal(2, guide.Reached);
        Assert.True(guide.Update(C));
        Assert.True(guide.Finished);
        Assert.Null(guide.Target);
    }

    // ---- loading ----

    private static readonly QuestRouteGroups Groups = new(MarkerSet: 24, Quest: 31, Actor: 1);

    private static int? Resolve(int group, string name) => (group, name) switch
    {
        (24, "Kehj_Ridge (Kehj_Ridge_Subzone_EliasPalace_Sandstorm)") => 577564,
        (31, "VGN_Kehj_EliasPalace_Sandstorm") => 577560,
        (31, "QST_Kehj_EliasPalace_03") => 574673,
        (1, "BCD_Trigger_EliasPalace_SafeZone") => 972175,
        (1, "NPC_QST_Kehj_EliasPalace_Guide") => GuideActor,
        _ => null,
    };

    private static IReadOnlyList<PlacedMarker> SandstormMarkers(int markerSet) =>
    [
        new(1, 972175, new Vector3(C, 26.9f)),
        new(1, GuideActor, new Vector3(C.X - 2, C.Y + 3, 26.2f)),
        new(31, 577560, new Vector3(Start, 39f)),
        new(1, 972175, new Vector3(A, 47f)),
        new(1, 41245, new Vector3(-500, 0, 30)),
        new(1, 972175, new Vector3(B, 33.1f)),
    ];

    // The next quest's scripts name Meshif on the palace steps and a noble; the route's end is the Meshif marker.
    private static IReadOnlyList<PlacedMarker> NextQuestMarkers(int quest) => quest == 574673
        ? [new(1, GuideActor, new Vector3(PalaceSteps, 50f)), new(1, OtherActor, new Vector3(-720.1f, -41.1f, 50f))]
        : [];

    [Fact]
    public void Loading_takes_the_quest_marker_as_start_the_next_quests_guide_as_end_and_orders_the_target_markers()
    {
        var route = QuestRouteLoader.Load(QuestRouteCatalog.SandstormShelters, Groups, Resolve, SandstormMarkers, NextQuestMarkers, _ => { });

        Assert.NotNull(route);
        Assert.Equal(Start, route.Start);
        Assert.Equal([B, C, A], route.Points);
        Assert.Equal(PalaceSteps, route.End);
        Assert.Equal([B, C, A, PalaceSteps], route.Path);
        Assert.True(route.ActivationSnos.SetEquals([GuideActor, 972175])); // the other names aren't resolvable here
    }

    [Fact]
    public void Without_a_single_end_marker_the_route_ends_at_its_last_point_with_a_log_line()
    {
        var log = new List<string>();
        var route = QuestRouteLoader.Load(QuestRouteCatalog.SandstormShelters, Groups, Resolve, SandstormMarkers, _ => [], log.Add);

        Assert.NotNull(route);
        Assert.Null(route.End);
        Assert.Equal([A, B, C], route.Points);
        Assert.Single(log);
    }

    [Fact]
    public void A_missing_name_or_quest_marker_turns_the_route_off_with_a_log_line()
    {
        var log = new List<string>();
        Assert.Null(QuestRouteLoader.Load(QuestRouteCatalog.SandstormShelters, Groups, (_, _) => null, SandstormMarkers, NextQuestMarkers, log.Add));
        Assert.Null(QuestRouteLoader.Load(
            QuestRouteCatalog.SandstormShelters, Groups, Resolve, set => [.. SandstormMarkers(set).Where(m => m.Group != 31)],
            NextQuestMarkers, log.Add));
        Assert.Equal(2, log.Count);
    }

    // ---- bridge ----

    private sealed class Rig
    {
        public FakeFeed Feed = new();
        public FakeSpeech Speech = new();
        public FakePad Pad = new();
        public AudioMixer Mixer = new();
        public GuidePingPlayer Ping;
        public double Now;

        public Rig(LoadedQuestRoute? route = null)
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            route ??= new LoadedQuestRoute(QuestRouteCatalog.SandstormShelters, new HashSet<int> { GuideActor }, Start, [A, B, C], PalaceSteps);
            _ = new QuestRouteBridge(Feed, [route], Ping, Speech, pad: Pad);
            Feed.RaiseWorld(World);
        }

        private Vector2 _last;

        public void Move(Vector2 p) { Now += 0.1; _last = p; Feed.RaisePlayer(p); }

        // Moves past any pending ping or chime, re-reports the last position (a live poll) and says whether the guide ping plays.
        public bool Pings()
        {
            Now += 2;
            Mixer.Render(new short[AudioFormat.SampleRate * AudioFormat.Channels], AudioFormat.SampleRate); // drain earlier sounds
            Feed.RaisePlayer(_last);
            Ping.Tick();
            var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
            Mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
            return buffer.Any(s => s != 0);
        }
    }

    [Fact]
    public void A_quest_actor_in_view_starts_the_route_and_the_ping_leads_to_the_first_shelter()
    {
        var rig = new Rig();
        rig.Move(new Vector2(-520, 40));
        Assert.False(rig.Pings());

        rig.Feed.RaiseActors(new ActorState(1, OtherActor, -520, 30, 0));
        Assert.Empty(rig.Speech.Spoken);

        rig.Feed.RaiseActors(new ActorState(2, GuideActor, -573, 38, 47));
        rig.Move(new Vector2(-521, 40));

        Assert.Equal(["Guía de refugios."], rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void The_guide_at_another_quests_place_never_starts_the_route()
    {
        // The drug den on the riverbank, far from the route: no start, so no start-and-stop on every poll.
        var rig = new Rig();
        rig.Move(new Vector2(-60, 90));
        for (int i = 0; i < 3; i++) rig.Feed.RaiseActors(new ActorState(2, GuideActor, -63.0f, 93.3f, 0));

        // The Forsaken Chapel, inside the route's area but 67 units from the nearest shelter.
        rig.Move(new Vector2(-430, -70));
        rig.Feed.RaiseActors(new ActorState(2, GuideActor, -422.9f, -72.0f, 0));

        Assert.Empty(rig.Speech.Spoken);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void Standing_at_the_quest_marker_starts_the_route_without_any_actor()
    {
        var rig = new Rig();

        rig.Move(Start + new Vector2(3, 3));

        Assert.Equal(["Guía de refugios."], rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void The_ping_waits_at_a_shelter_and_leads_on_once_the_player_steps_out()
    {
        var rig = new Rig();
        rig.Move(Start);

        rig.Move(A);
        Assert.False(rig.Pings());
        rig.Move(A + new Vector2(0, -6)); // still inside: the storm may be blowing
        Assert.False(rig.Pings());

        rig.Move(A + new Vector2(8, -12));
        Assert.True(rig.Pings());
        Assert.Equal(["Guía de refugios.", "Refugio 1 de 3."], rig.Speech.Texts);
    }

    [Fact]
    public void Starting_while_standing_at_a_shelter_waits_there_too()
    {
        var rig = new Rig();
        rig.Move(B + new Vector2(2, 0));

        rig.Feed.RaiseActors(new ActorState(2, GuideActor, B.X, B.Y, 0));

        Assert.Equal(["Guía de refugios."], rig.Speech.Texts);
        Assert.False(rig.Pings());
        rig.Move(B + new Vector2(0, -15)); // toward C
        Assert.True(rig.Pings());
    }

    [Fact]
    public void After_the_last_shelter_the_ping_leads_on_to_the_end_and_the_finished_route_does_not_restart()
    {
        var rig = new Rig();
        rig.Move(Start);

        rig.Move(A);
        rig.Move(B);
        rig.Move(C);
        Assert.False(rig.Pings()); // waiting out the last storm
        rig.Move(C + new Vector2(-12, 12)); // following Meshif toward the palace
        Assert.True(rig.Pings());

        rig.Move(PalaceSteps);
        rig.Feed.RaiseActors(new ActorState(2, GuideActor, C.X, C.Y, 0));
        rig.Move(C + new Vector2(1, 0));

        Assert.Equal(
            ["Guía de refugios.", "Refugio 1 de 3.", "Refugio 2 de 3.", "Refugio 3 de 3.", "Fin de la guía de refugios."], rig.Speech.Texts);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void Leaving_the_area_or_the_world_stops_the_ping()
    {
        var rig = new Rig();
        rig.Move(Start);
        rig.Move(new Vector2(-900, 45)); // far north of every point
        Assert.False(rig.Pings());

        rig.Move(Start);
        Assert.True(rig.Pings());
        rig.Feed.RaiseWorld("DGN_Kehj_HakansRefuge");
        rig.Move(Start);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void Dpad_left_mutes_and_unmutes_a_leading_route_and_does_nothing_otherwise()
    {
        var rig = new Rig();
        rig.Pad.Press();
        Assert.Empty(rig.Speech.Spoken);

        rig.Move(Start);
        rig.Pad.Press();
        rig.Move(Start + new Vector2(-10, 0));
        Assert.False(rig.Pings());

        rig.Pad.Press();
        rig.Move(Start + new Vector2(-20, 0));
        Assert.True(rig.Pings());
        Assert.Equal(["Guía de refugios.", "Guía desactivada.", "Guía activada."], rig.Speech.Texts);
    }

    [Fact]
    public void A_muted_route_that_stops_restarts_audible()
    {
        var rig = new Rig();
        rig.Move(Start);
        rig.Pad.Press();
        Assert.False(rig.Pings());

        rig.Move(new Vector2(-900, 45)); // left the area: the route stops
        rig.Move(Start);
        Assert.True(rig.Pings());

        rig.Pad.Press();
        rig.Feed.RaiseWorld("DGN_Kehj_HakansRefuge");
        rig.Feed.RaiseWorld(World);
        rig.Move(Start);
        Assert.True(rig.Pings());
        Assert.Equal(["Guía de refugios.", "Guía desactivada.", "Guía de refugios.", "Guía desactivada.", "Guía de refugios."], rig.Speech.Texts);
    }

    [Fact]
    public void Other_worlds_never_start_the_route()
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld("Kehj_Hell");

        rig.Feed.RaiseActors(new ActorState(2, GuideActor, Start.X, Start.Y, 0));
        rig.Move(Start);

        Assert.Empty(rig.Speech.Spoken);
        Assert.False(rig.Pings());
    }

    // ---- trail (Lilith's blood petals, "Auf blutigem Pfad") ----

    // The first petals of Skov_Philios_Campaign_Design as the game data places them, about 10 units apart, and the last.
    private static readonly Vector2 P1 = new(2310.5f, -441.9f), P2 = new(2301.5f, -449.0f), P3 = new(2295.0f, -441.5f),
        P4 = new(2295.9f, -430.4f), PLast = new(2382.7f, -320.0f);
    private const int Petal = 2557169;

    private static LoadedQuestRoute Trail() =>
        new(QuestRouteCatalog.PhiliosBloodPetals, new HashSet<int> { Petal }, P1, [P1, P2, P3, P4, PLast]);

    [Fact]
    public void A_trail_starts_at_its_first_marker_and_keeps_the_marker_sets_order()
    {
        int? resolve(int group, string name) => (group, name) switch
        {
            (24, "Skov_Philios (Skov_Philios_Campaign_Design)") => 1943891,
            (1, "QST_X2_Skov_Lilith_BloodPetal_Trigger_Sphere") => Petal,
            _ => null,
        };
        IReadOnlyList<PlacedMarker> markers(int set) =>
        [
            new(1, Petal, new Vector3(P1, 17)), new(1, 41245, new Vector3(2300, -400, 10)), new(1, Petal, new Vector3(P2, 17)),
            new(1, Petal, new Vector3(P3, 16)), new(1, Petal, new Vector3(P4, 14)), new(1, Petal, new Vector3(PLast, 4)),
        ];

        var route = QuestRouteLoader.Load(QuestRouteCatalog.PhiliosBloodPetals, Groups, resolve, markers, _ => [], _ => { });

        Assert.NotNull(route);
        Assert.Equal(P1, route.Start);
        Assert.Equal([P1, P2, P3, P4, PLast], route.Points); // the set lists a trail in walking order
        Assert.True(route.ActivationSnos.SetEquals([Petal]));
    }

    [Fact]
    public void A_petal_in_view_starts_the_trail_which_leads_on_without_waiting_and_speaks_only_its_end()
    {
        var rig = new Rig(Trail());
        rig.Move(P1 + new Vector2(5, 3));
        Assert.Empty(rig.Speech.Spoken); // no quest marker to stand at: only a petal starts a trail

        rig.Feed.RaiseActors(new ActorState(1, Petal, P1.X, P1.Y, 17));
        rig.Move(P1 + new Vector2(4, 3));
        Assert.Equal(["Guía del rastro de pétalos."], rig.Speech.Texts);
        Assert.True(rig.Pings());

        rig.Move(P2 + new Vector2(0, 5)); // 5 units beside the petal
        Assert.True(rig.Pings()); // straight on to P3, no hold
        rig.Move(P4);
        Assert.True(rig.Pings());
        Assert.Equal(["Guía del rastro de pétalos."], rig.Speech.Texts);

        rig.Move(PLast);
        Assert.Equal(["Guía del rastro de pétalos.", "Fin del rastro."], rig.Speech.Texts);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void A_petal_of_another_quest_far_away_does_not_start_the_trail()
    {
        var rig = new Rig(Trail());
        rig.Move(new Vector2(2752.9f, -44.8f)); // Lycander's petal trail

        rig.Feed.RaiseActors(new ActorState(1, Petal, 2752.9f, -44.8f, 5));
        rig.Move(new Vector2(2753, -45));

        Assert.Empty(rig.Speech.Spoken);
        Assert.False(rig.Pings());
    }

    // ---- pentagram ("Hexenmeister: Bauch der Bestie", "Folgt dem Pentagramm.") ----

    // The start and the first stroke as Step_WesternFlats (QST_Warlock_ClassQ_2a) places them, then a stroke crossing the
    // first one 0.4 units from its second point.
    private static readonly Vector2 S0 = new(-699.6f, -938.2f), S1 = new(-699.5f, -935.3f), S2 = new(-699.4f, -937.8f),
        S3 = new(-699.3f, -940.3f), S4 = new(-699.2f, -942.5f), S5 = new(-701.9f, -937.2f), S6 = new(-699.4f, -937.4f),
        S7 = new(-696.9f, -937.6f);
    private const int Sacrifice = 2538891;

    private static LoadedQuestRoute Pentagram() =>
        new(QuestRouteCatalog.WarlockPentagram, new HashSet<int> { Sacrifice }, S0, [S0, S1, S2, S3, S4, S5, S6, S7]);

    [Fact]
    public void The_pentagram_starts_only_once_the_sacrifice_is_used()
    {
        var rig = new Rig(Pentagram());
        rig.Move(S0 + new Vector2(10, 0));

        rig.Feed.RaiseActors(new ActorState(1, Sacrifice, -698.5f, -936.4f, 9));
        rig.Move(S0 + new Vector2(9, 0));
        Assert.Empty(rig.Speech.Spoken);

        rig.Feed.RaiseActors(new ActorState(1, Sacrifice, -698.5f, -936.4f, 9, Operated: true));
        rig.Move(S0 + new Vector2(8, 0));
        Assert.Equal(["Guía del pentagrama."], rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void The_pentagram_is_traced_point_by_point_where_its_strokes_cross()
    {
        var rig = new Rig(Pentagram());
        rig.Move(S2); // standing where two strokes cross: the trace still starts at its first point
        rig.Feed.RaiseActors(new ActorState(1, Sacrifice, -698.5f, -936.4f, 9, Operated: true));
        foreach (var p in new[] { S0, S1, S2 }) rig.Move(p);

        rig.Move(S6); // 0.4 units from S2: the crossing stroke's point isn't taken before S3..S5
        rig.Move(S3);
        rig.Move(S4);
        rig.Move(S5);
        Assert.Equal(["Guía del pentagrama."], rig.Speech.Texts);
        rig.Move(S6);
        rig.Move(S7);

        Assert.Equal(["Guía del pentagrama.", "Fin del pentagrama."], rig.Speech.Texts);
    }

    // ---- fakes ----

    private sealed class FakeFeed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; }
        public WorldKind CurrentWorldKind { get; set; } = WorldKind.Overworld;

        public void RaiseWorld(string name) => WorldChanged?.Invoke(1, name, false);
        public void RaisePlayer(Vector2 p) => PlayerMoved?.Invoke(new PlayerState(p.X, p.Y, 0, 0));
        public void RaiseTiles(params TileState[] tiles) => TilesUpdated?.Invoke(tiles);
        public void RaiseActors(params ActorState[] actors) => ActorsUpdated?.Invoke(actors);
    }

    private sealed class FakeSpeech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public IEnumerable<string> Texts => Spoken.Select(s => s.Text);
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private sealed class FakePad : IGuideTogglePad
    {
        public event Action? DpadLeftPressed;
        public void Press() => DpadLeftPressed?.Invoke();
    }
}
