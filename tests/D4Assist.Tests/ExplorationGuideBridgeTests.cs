using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;

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

        public Rig(bool guideInDungeons = false)
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            _ = new ExplorationGuideBridge(Feed, (_, _, _) => Step, Ping, Speech, () => BeaconActive, Log.Add, Pad, guideInDungeons);
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
        _ = new ExplorationGuideBridge(feed, (_, _, _) => step, ping, new FakeSpeech(), () => false);

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
    public void Press_outside_a_dungeon_is_ignored()
    {
        var rig = new Rig();
        rig.Feed.RaiseWorld("Sanctuary_Hub", isDungeon: false);
        rig.Pad.Press();
        rig.Feed.RaiseWorld("X1_Undercity_B");
        rig.Feed.RaisePlayer();

        Assert.Empty(rig.Speech.Spoken);
        Assert.True(rig.Hear().Right > 0);
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
}
