using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class ExplorationGuideBridgeTests
{
    private sealed class FakeFeed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; }

        public void RaiseWorld(string name = "X1_Undercity_Test", bool isDungeon = true) => WorldChanged?.Invoke(0, name, isDungeon);
        public void RaiseTiles(params string[] names) =>
            TilesUpdated?.Invoke(names.Select(n => new TileState(1, n, 0, 0, 32, 32, 0, 1, 1)).ToList());
        public void RaisePlayer(float x = 0, float y = 0) => PlayerMoved?.Invoke(new PlayerState(x, y, 0, 0));
    }

    private sealed class FakeSpeech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private sealed class FakePad : IGuideTogglePad
    {
        public event Action? DpadLeftPressed;
        public void Press() => DpadLeftPressed?.Invoke();
        public event Action? DpadLeftHeld;
        public void Hold() => DpadLeftHeld?.Invoke();
    }

    private sealed class Rig
    {
        public FakePad Pad = new();
        public FakeFeed Feed = new();
        public FakeSpeech Speech = new();
        public AudioMixer Mixer = new();
        public GuidePingPlayer Ping;
        public GuideStep Step = new(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Frontier, "a", (-10, 10)), (-10, 10), 50);
        public bool BeaconActive;
        public double Now;
        public List<string> Log = [];
        public List<GuideDestination> Objectives = [];
        public IReadOnlyList<GuideDestination>? AskedObjectives;
        public int Rearms;
        public bool RouteActive;

        public Rig(bool guideInDungeons = false)
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            _ = new ExplorationGuideBridge(
                Feed, (_, _, _, objectives) => { AskedObjectives = objectives; return Step; }, Ping, Speech, () => BeaconActive, Log.Add, Pad,
                guideInDungeons, objectives: () => Objectives, rearmObjectives: _ => Rearms++, routeActive: () => RouteActive);
            Feed.RaiseWorld();
        }

        // Ticks once and reports the (left, right) energy of what plays.
        public (double Left, double Right) Hear()
        {
            Ping.Tick();
            var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
            Mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
            double left = 0, right = 0;
            for (int i = 0; i < buffer.Length; i += 2) { left += Math.Abs(buffer[i]); right += Math.Abs(buffer[i + 1]); }
            return (left, right);
        }
    }

    [Fact]
    public void App_started_inside_an_endgame_dungeon_guides_without_waiting_for_a_world_change()
    {
        var feed = new FakeFeed { CurrentWorld = ("X1_Undercity_BugCave_03", true) };
        var mixer = new AudioMixer();
        var ping = new GuidePingPlayer(mixer, () => 0, autoTick: false);
        var step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Frontier, "a", (-10, 10)), (-10, 10), 50);
        _ = new ExplorationGuideBridge(feed, (_, _, _, _) => step, ping, new FakeSpeech(), () => false);

        feed.RaisePlayer();
        ping.Tick();
        var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
        mixer.Render(buffer, buffer.Length / AudioFormat.Channels);

        Assert.Contains(buffer, sample => sample != 0);
    }

    [Fact]
    public void Guiding_pings_towards_the_waypoints_screen_side()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer(); // waypoint at engine (-10, +10): screen right

        var (left, right) = rig.Hear();

        Assert.True(right > left * 5);
    }

    [Fact]
    public void A_waypoint_on_the_other_side_pans_the_other_way()
    {
        var rig = new Rig { Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Frontier, "a", (10, -10)), (10, -10), 50) };
        rig.Feed.RaisePlayer();

        var (left, right) = rig.Hear();

        Assert.True(left > right * 5);
    }

    [Fact]
    public void Reaching_a_target_plays_a_centred_chime_once()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Hear(); // the first ping (hard right)

        rig.Step = rig.Step with { Arrivals = 1 };
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();
        var (left, right) = rig.Hear(); // the next ping isn't due yet: only the chime sounds

        Assert.True(left > 0);
        Assert.InRange(right / left, 0.9, 1.1);
    }

    [Fact]
    public void Targets_reached_while_the_beacon_leads_do_not_chime_late()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Hear();

        rig.BeaconActive = true;
        rig.Step = rig.Step with { Arrivals = 1 };
        rig.Feed.RaisePlayer();
        rig.BeaconActive = false;
        rig.Feed.RaisePlayer();
        var (left, _) = rig.Hear();

        Assert.Equal(0, left); // only the hard-right ping could sound, never the centred chime
    }

    [Fact]
    public void Undercity_boss_room_turns_the_guide_off_until_the_world_changes()
    {
        var rig = new Rig { Step = GuideStep.AllExplored };
        rig.Feed.RaiseTiles("Cave_Bug_FLEX_POI_BossRoom_Undercity", "Cave_Bug_W_01");
        rig.Feed.RaisePlayer();
        Assert.Equal((0, 0), rig.Hear());
        Assert.Empty(rig.Speech.Spoken);
        Assert.Contains(rig.Log, line => line.Contains("boss room"));

        rig.Feed.RaiseWorld();
        rig.Feed.RaisePlayer();
        Assert.Single(rig.Speech.Spoken);
    }

    [Fact]
    public void Beacon_target_mutes_the_guide_and_it_resumes_when_the_beacon_is_gone()
    {
        var rig = new Rig { BeaconActive = true };
        rig.Feed.RaisePlayer();
        Assert.Equal((0, 0), rig.Hear());

        rig.BeaconActive = false;
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);
    }

    [Fact]
    public void Beacon_appearing_mid_route_silences_the_ping()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);

        rig.BeaconActive = true;
        rig.Feed.RaisePlayer();
        rig.Now = 10; // long past the next scheduled ping

        Assert.Equal((0, 0), rig.Hear());
    }

    [Fact]
    public void All_explored_is_silent_and_spoken_once_without_interrupting()
    {
        var rig = new Rig { Step = GuideStep.AllExplored };

        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();
        rig.Feed.RaiseTiles();

        Assert.Equal((0, 0), rig.Hear());
        Assert.Equal([(NavigationPhrases.Spanish.AllExplored, false)], rig.Speech.Spoken);
        Assert.Equal("Todo explorado.", NavigationPhrases.Spanish.AllExplored);
    }

    [Fact]
    public void All_explored_lets_what_was_left_undone_lead_again_once_per_target_reached()
    {
        // Live 2026-10-17, the Light's Watch: the key taken, the gate stayed tried and the guide fell silent.
        var rig = new Rig { Step = GuideStep.AllExplored };
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Pad.Press(); // on: one re-arm of its own
        rig.Speech.Spoken.Clear();

        rig.Feed.RaisePlayer();
        Assert.Equal(2, rig.Rearms);
        Assert.Single(rig.Log, l => l.Contains("GUIDE all explored"));
        Assert.Equal([(NavigationPhrases.Spanish.AllExplored, false)], rig.Speech.Spoken); // nothing came back to lead to

        rig.Feed.RaisePlayer();
        Assert.Equal(2, rig.Rearms);

        rig.Step = GuideStep.AllExplored with { Arrivals = 1 }; // the player went to the gate and came away again
        rig.Feed.RaisePlayer();
        Assert.Equal(3, rig.Rearms);
    }

    [Fact]
    public void All_explored_in_an_endgame_world_re_arms_nothing()
    {
        var rig = new Rig { Step = GuideStep.AllExplored };

        rig.Feed.RaisePlayer();

        Assert.Equal(0, rig.Rearms);
    }

    [Fact]
    public void A_new_dungeon_stops_the_ping_and_lets_all_explored_be_spoken_again()
    {
        var rig = new Rig { Step = GuideStep.AllExplored };
        rig.Feed.RaisePlayer();

        rig.Feed.RaiseWorld();
        rig.Feed.RaisePlayer();

        Assert.Equal(2, rig.Speech.Spoken.Count);
    }

    [Fact]
    public void World_change_silences_the_ping_until_the_next_player_update()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Feed.RaiseWorld();

        Assert.Equal((0, 0), rig.Hear());
    }

    [Fact]
    public void Unknown_position_is_silent_and_speaks_nothing()
    {
        var rig = new Rig { Step = GuideStep.Unknown };
        rig.Feed.RaisePlayer();

        Assert.Equal((0, 0), rig.Hear());
        Assert.Empty(rig.Speech.Spoken);
    }

    [Fact]
    public void Logs_one_line_per_target_change_never_per_poll()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();
        rig.Feed.RaiseTiles();
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.UnvisitedTile, "b", (5, 6)), (5, 6), 12);
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();

        Assert.Equal(2, rig.Log.Count);
        Assert.Contains("Frontier", rig.Log[0]);
        Assert.Contains("UnvisitedTile", rig.Log[1]);
        Assert.Contains("route=12", rig.Log[1]);
    }

    [Theory]
    [InlineData("X1_Undercity_Test", true)]
    [InlineData("PIT_Test", true)]
    [InlineData("DGN_Test", false)]
    public void Default_depends_on_the_world_kind(string world, bool audible)
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld(world);
        rig.Feed.RaisePlayer();

        Assert.Equal(audible, rig.Hear().Right > 0);
    }

    [Fact]
    public void Config_can_turn_the_guide_on_in_other_dungeons()
    {
        var rig = new Rig(guideInDungeons: true);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Feed.RaisePlayer();

        Assert.True(rig.Hear().Right > 0);
    }

    [Fact]
    public void Toggle_flips_only_the_current_kind_and_persists_across_worlds_of_that_kind()
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld("DGN_A");
        rig.Pad.Press(); // dungeons on
        rig.Feed.RaiseWorld("DGN_B");
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);

        rig.Now += 10;
        rig.Feed.RaiseWorld("PIT_A"); // endgame untouched: still on
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);
        rig.Pad.Press(); // endgame off
        rig.Feed.RaiseWorld("X1_Undercity_B");
        rig.Feed.RaisePlayer();
        Assert.Equal((0, 0), rig.Hear());

        rig.Now += 10;
        rig.Feed.RaiseWorld("DGN_C"); // dungeons still on
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);
    }

    [Fact]
    public void Toggle_speaks_the_confirmation_with_interrupt_and_logs_it()
    {
        var rig = new Rig();
        rig.Pad.Press();
        rig.Pad.Press();

        Assert.Equal([("Guía desactivada.", true), ("Guía activada.", true)], rig.Speech.Spoken);
        Assert.Equal(2, rig.Log.Count(l => l.Contains("GUIDE toggled")));
    }

    [Fact]
    public void Press_outside_a_dungeon_says_the_guide_is_for_dungeons_and_changes_nothing()
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld("Sanctuary_Hub", isDungeon: false);
        rig.Pad.Press();
        Assert.Equal([(AppStrings.Spanish.GuideDungeonsOnly, true)], rig.Speech.Spoken);

        rig.Feed.RaiseWorld("X1_Undercity_B");
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);
    }

    [Fact]
    public void Press_outside_a_dungeon_is_left_to_a_leading_quest_route()
    {
        var rig = new Rig { RouteActive = true };
        rig.Feed.RaiseWorld("Kehj", isDungeon: false);
        rig.Pad.Press();

        Assert.Empty(rig.Speech.Spoken);
    }

    [Fact]
    public void While_off_nothing_sounds_or_speaks_and_switching_on_does_not_chime_late()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Pad.Press(); // off
        rig.Speech.Spoken.Clear();
        rig.Step = GuideStep.AllExplored;
        rig.Feed.RaisePlayer();
        rig.Feed.RaiseTiles();
        Assert.Equal((0, 0), rig.Hear());
        Assert.Empty(rig.Speech.Spoken);

        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Frontier, "a", (-10, 10)), (-10, 10), 50) { Arrivals = 3 };
        rig.Pad.Press(); // on
        var (left, _) = rig.Hear();

        Assert.Equal(0, left); // only the hard-right ping, never the centred arrival chime
    }

    private static GuideStep ToObjective(string id) =>
        new(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Objective, id, (-10, 10)), (-10, 10), 50);

    [Fact]
    public void On_a_quest_place_the_guide_chimes_once_and_holds_until_the_player_walks_off()
    {
        // Live 2026-10-01: reaching phase 154's trigger turned the guide to unexplored ground while the step went on there.
        string id = $"{ObjectiveMemory.QuestPlaceKind}:42298:0,3";
        var rig = new Rig { Objectives = [new(id, (0, 3), ObjectiveMemory.QuestTier, Radius: ExplorationGuide.DestinationArrivalUnits)] };
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Objective, id, (0, 3)), (0, 3), 3);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Pad.Press(); // on

        rig.Feed.RaisePlayer(0, 0);  // 3 units off: on it
        rig.Feed.RaisePlayer(0, -6); // 9 units: still within the release margin
        Assert.Single(rig.Log, l => l.Contains($"GUIDE holding at {id}"));

        rig.Feed.RaisePlayer(0, -20); // walked off: it leads back to the place
        rig.Now += 1;
        Assert.True(rig.Hear().Right > 0 || rig.Hear().Left > 0);
        rig.Feed.RaisePlayer(0, 0);
        Assert.Equal(2, rig.Log.Count(l => l.Contains($"GUIDE holding at {id}")));
    }

    [Fact]
    public void At_an_objective_heard_earlier_the_guide_holds_instead_of_turning_to_unexplored_ground()
    {
        // Players' reports (2026-10-05): the guide led to the objective, then on to exploring before it was used.
        string id = "objective:1:0,3";
        var rig = new Rig { Objectives = [new(id, (0, 3), ObjectiveMemory.DungeonTier, Radius: 4)] };
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Objective, id, (0, 3)), (0, 3), 3);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Pad.Press(); // on

        rig.Feed.RaisePlayer(0, 0);
        rig.Feed.RaisePlayer(0, 1);

        Assert.Single(rig.Log, l => l.Contains($"GUIDE holding at {id}"));
    }

    [Fact]
    public void Exploring_towards_an_objective_the_map_marks_is_spoken_as_leading_to_it()
    {
        var rig = new Rig(guideInDungeons: true);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.UnvisitedTile, "t", (5, 6)), (5, 6), 12,
            Toward: new("objective:1:0,300", (0, 300), ObjectiveMemory.QuestTier));

        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();

        Assert.Equal([(NavigationPhrases.Spanish.ToQuestObjective, false)], rig.Speech.Spoken);
    }

    [Fact]
    public void Over_a_quest_place_on_another_floor_the_guide_says_it_is_lower_and_keeps_pointing()
    {
        // Live 2026-10-01: the Rathma temple's Trigger_Sphere_8m (radius 4) lay 23 units below the walkway over it.
        string id = $"{ObjectiveMemory.QuestPlaceKind}:42298:0,2";
        var rig = new Rig { Objectives = [new(id, (0, 2), ObjectiveMemory.QuestTier, Z: -23, Radius: 4)] };
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Objective, id, (0, 2)), (0, 2), 2);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Pad.Press(); // on
        rig.Speech.Spoken.Clear();

        rig.Feed.RaisePlayer(0, 0);
        rig.Feed.RaisePlayer(0, 1);

        Assert.Single(rig.Speech.Spoken, s => s.Text == NavigationPhrases.Spanish.QuestObjectiveBelow); // once, after the goal
        Assert.DoesNotContain(rig.Log, l => l.Contains("GUIDE holding"));

        rig.Objectives = [new(id, (0, 2), ObjectiveMemory.QuestTier, Z: 1, Radius: 4)]; // down on its floor
        rig.Feed.RaisePlayer(0, 0);
        Assert.Single(rig.Log, l => l.Contains($"GUIDE holding at {id}"));
    }

    [Fact]
    public void Switching_on_says_where_the_guide_leads()
    {
        var rig = new Rig { Objectives = [new("q", (-10, 10), ObjectiveMemory.QuestTier)], Step = ToObjective("q") };
        rig.Feed.RaiseWorld("DGN_Test"); // off by default
        rig.Feed.RaisePlayer();
        Assert.Empty(rig.Speech.Spoken);

        rig.Pad.Press();

        Assert.Equal([("Guía activada.", true), (NavigationPhrases.Spanish.ToQuestObjective, false)], rig.Speech.Spoken);
        Assert.True(rig.Hear().Right > 0);
        Assert.Equal(1, rig.Rearms); // objectives walked past lead again

        rig.Pad.Press();
        Assert.Equal(1, rig.Rearms);
    }

    [Fact]
    public void A_floor_starts_exploring_silently_and_turning_to_an_objective_and_back_is_spoken_once_each()
    {
        var rig = new Rig(guideInDungeons: true);
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Feed.RaisePlayer();
        Assert.Empty(rig.Speech.Spoken);

        rig.Objectives = [new("d", (-10, 10), ObjectiveMemory.DungeonTier)];
        rig.Step = ToObjective("d");
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();
        rig.Objectives = [];
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.UnvisitedTile, "t", (5, 6)), (5, 6), 12);
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();

        Assert.Equal([(NavigationPhrases.Spanish.ToDungeonObjective, false), (NavigationPhrases.Spanish.ToUnexplored, false)], rig.Speech.Spoken);
        Assert.Equal("Hacia zona sin explorar.", NavigationPhrases.Spanish.ToUnexplored);
    }

    [Fact]
    public void The_boss_rooms_entrance_and_middle_are_announced_once()
    {
        var rig = new Rig(guideInDungeons: true);
        rig.Feed.RaiseWorld("DGN_Test");
        string room = $"{BossRoomDestination.IdPrefix}:20:-32,-128";
        foreach (string id in new[] { $"{room}:entrance", room })
        {
            rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Destination, id, (-10, 10)), (-10, 10), 30);
            rig.Feed.RaisePlayer();
        }

        Assert.Equal([(D4Assist.Strings.AppStrings.Spanish.GuideToBossRoom, false)], rig.Speech.Spoken);
    }

    [Fact]
    public void Endgame_runs_never_lead_to_objectives()
    {
        var rig = new Rig { Objectives = [new("d", (-10, 10), ObjectiveMemory.DungeonTier)] }; // Undercity

        rig.Feed.RaisePlayer();

        Assert.NotNull(rig.AskedObjectives);
        Assert.Empty(rig.AskedObjectives!);
    }

    [Fact]
    public void A_waiting_objective_keeps_leading_while_an_unused_portal_is_in_sight()
    {
        var rig = new Rig(guideInDungeons: true) { BeaconActive = true, Objectives = [new("d", (-10, 10), ObjectiveMemory.DungeonTier)] };
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Step = ToObjective("d");
        rig.Feed.RaisePlayer();
        Assert.True(rig.Hear().Right > 0);

        rig.Now += 10;
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Frontier, "a", (-10, 10)), (-10, 10), 50); // objective cut off
        rig.Feed.RaisePlayer();
        Assert.Equal((0, 0), rig.Hear());
    }

    [Fact]
    public void Holding_d_pad_left_switches_between_the_objectives_and_exploring_and_a_new_world_starts_on_the_objectives()
    {
        // Tester, 2026-10-27: a campaign dungeon's objective kept leading back; "a way to toggle between moving you to objectives
        // and moving you to unexplored areas".
        var rig = new Rig { Objectives = [new("d", (-10, 10), ObjectiveMemory.QuestTier)] };
        rig.Feed.RaiseWorld("DGN_Test");
        rig.Feed.RaisePlayer();
        Assert.Empty(rig.AskedObjectives ?? []); // off by default outside the endgame

        rig.Pad.Hold();
        Assert.Equal(AppStrings.Spanish.GuideExploreMode, rig.Speech.Spoken[^1].Text);
        Assert.Empty(rig.AskedObjectives!); // on, past the objectives

        rig.Pad.Hold();
        Assert.Contains((AppStrings.Spanish.GuideObjectivesMode, true), rig.Speech.Spoken); // then where it leads
        Assert.Single(rig.AskedObjectives!);
        Assert.Equal(1, rig.Rearms); // back on the objectives, those walked away from lead again

        rig.Pad.Hold();
        rig.Feed.RaiseWorld("DGN_Next");
        rig.Feed.RaisePlayer();
        rig.Pad.Press(); // on again
        Assert.Single(rig.AskedObjectives!);
    }

    [Fact]
    public void Holding_d_pad_left_outside_a_dungeon_says_the_guide_works_in_dungeons_only()
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld("Sanctuary_Eastern_Continent", isDungeon: false);

        rig.Pad.Hold();

        Assert.Equal(AppStrings.Spanish.GuideDungeonsOnly, rig.Speech.Spoken[^1].Text);
    }
}
