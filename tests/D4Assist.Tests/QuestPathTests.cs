using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Data.Readers;

namespace D4Assist.Tests;

public class QuestPathTests
{
    private const string World = "Sanctuary_Eastern_Continent";
    private const int Warlock = 2449612, Fruits = 2399999, Sphere = 42298, Corpse = 1870562, Shrine = 1093480;
    private static readonly Vector2 KedBardu = new(-886f, -990f);
    private static readonly Vector2 LostPlains = new(-963.8f, -1024.9f); // the warlock quest's marked step, 85 units off
    private static readonly Vector2 FarCorpse = new(-700f, -900f);       // another quest's marked object, 207 units off
    private static readonly AppStrings English = AppStrings.English;

    private static readonly MarkedActor[] Markers =
    [
        new(Shrine, -1007.3f, -1077.4f, 9.8f, 0, 11),
        new(Corpse, FarCorpse.X, FarCorpse.Y, 0, 0, 11),
        new(Sphere, LostPlains.X, LostPlains.Y, 11, 0, 11),
    ];

    // The quest scripts name the sphere and the corpse at their spots; the shrine is nobody's. A sphere of kind 13 stands for one
    // only a hidden callback names.
    private static IReadOnlyList<QuestMapPlace> QuestPlaces(IReadOnlyList<MarkedActor> markers) =>
    [
        .. markers.Where(m => m.ActorSno == Sphere).Select(m => new QuestMapPlace(Warlock, "Disciples of the Forbidden", m.ActorSno, new(m.X, m.Y),
            Hidden: m.Kind == 13)),
        .. markers.Where(m => m.ActorSno == Corpse).Select(m => new QuestMapPlace(Fruits, "By Their Fruits", m.ActorSno, new(m.X, m.Y))),
        .. markers.Where(m => m.Kind == 12).Select(m => new QuestMapPlace(0, "Quest marker", m.ActorSno, new(m.X, m.Y), GameMarker: true)),
    ];

    private sealed class Rig
    {
        public FakeFeed Feed = new();
        public FakeSpeech Speech = new();
        public FakePad Pad = new();
        public AudioMixer Mixer = new();
        public GuidePingPlayer Ping;
        public QuestPathBridge Bridge;
        public double Now;
        public TimeSpan Clock;
        public List<(string World, Vector3 From, Vector2 To)> Searches = [];
        // The path the search returns: straight from the player to the place in 5-unit steps, unless set.
        public Func<Vector3, Vector2, IReadOnlyList<Vector2>?> Path = (from, to) => Straight(new(from.X, from.Y), to);

        public Rig(string world = World)
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            Bridge = new QuestPathBridge(Feed, World, QuestPlaces, (w, from, to) => { Searches.Add((w, from, to)); return Path(from, to); },
                Ping, Speech, new LanguageSetting(SpeechLanguage.English), Pad, runInBackground: work => work(), clock: () => Clock);
            Feed.RaiseWorld(world);
            Bridge.OnMapMarkers(Markers);
        }

        private Vector2 _last;

        public void Move(Vector2 p) { Now += 0.1; Clock += TimeSpan.FromSeconds(0.1); _last = p; Feed.RaisePlayer(p); }

        // Moves past any pending ping or chime, re-reports the last position and says whether the guide ping plays.
        public bool Pings()
        {
            Now += 2;
            Mixer.Render(new short[AudioFormat.SampleRate * AudioFormat.Channels], AudioFormat.SampleRate);
            Feed.RaisePlayer(_last);
            Ping.Tick();
            var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
            Mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
            return buffer.Any(s => s != 0);
        }
    }

    private static List<Vector2> Straight(Vector2 from, Vector2 to)
    {
        var points = new List<Vector2>();
        float length = Vector2.Distance(from, to);
        for (float d = 5; d < length; d += 5) points.Add(Vector2.Lerp(from, to, d / length));
        points.Add(to);
        return points;
    }

    [Fact]
    public void D_pad_left_leads_to_the_nearest_quest_place_the_map_marks()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        Assert.True(rig.Bridge.TakesPad);
        Assert.False(rig.Pings());

        rig.Pad.Press();

        Assert.Equal("Quest goal: Disciples of the Forbidden, 85 meters. 1 of 2.", rig.Speech.Texts.Last());
        Assert.Equal((World, new Vector3(KedBardu, 0), LostPlains), Assert.Single(rig.Searches));
        Assert.True(rig.Pings());
    }

    [Fact]
    public void Further_presses_move_on_to_the_next_place_and_then_switch_it_off()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();

        rig.Pad.Press();
        Assert.Equal("Quest goal: By Their Fruits, 207 meters. 2 of 2.", rig.Speech.Texts.Last());
        Assert.Equal(FarCorpse, rig.Searches[^1].To);
        Assert.True(rig.Pings());

        rig.Pad.Press();
        Assert.Equal(English.GuideOff, rig.Speech.Texts.Last());
        Assert.False(rig.Pings());
    }

    [Fact]
    public void A_place_the_player_stands_at_is_skipped_for_the_next()
    {
        var rig = new Rig();
        rig.Move(LostPlains + new Vector2(2, 2)); // at the warlock's spot

        rig.Pad.Press();

        Assert.StartsWith("Quest goal: By Their Fruits,", rig.Speech.Texts.Last());
        Assert.Equal(FarCorpse, Assert.Single(rig.Searches).To);
    }

    [Fact]
    public void The_games_own_quest_marker_comes_before_nearer_quest_objects()
    {
        var rig = new Rig();
        var proxy = new Vector2(-1203.8f, -594.3f);
        rig.Move(KedBardu);
        rig.Bridge.OnMapMarkers([.. Markers, new(56602, proxy.X, proxy.Y, 47, 0, 12)]);

        rig.Pad.Press();

        Assert.Equal(proxy, rig.Searches[^1].To);
        Assert.StartsWith("Quest goal: Quest marker,", rig.Speech.Texts.Last());
    }

    [Fact]
    public void A_place_only_a_hidden_step_names_comes_after_the_shown_ones()
    {
        var rig = new Rig();
        var onTheWay = KedBardu + new Vector2(-20, 0);
        rig.Move(KedBardu);
        rig.Bridge.OnMapMarkers([.. Markers, new(Sphere, onTheWay.X, onTheWay.Y, 11, 0, 13)]);

        rig.Pad.Press();
        Assert.Equal(LostPlains, rig.Searches[^1].To);
        rig.Pad.Press();
        Assert.Equal(FarCorpse, rig.Searches[^1].To);

        rig.Pad.Press();
        Assert.Equal(onTheWay, rig.Searches[^1].To);
    }

    // "In Rathmas Fußstapfen" phase 12 (find the untouched forests): the trigger at the Rathmic statue is shown, the two on the way
    // there are hidden, and Tharra, who gave the quest at phase 0, is no longer the step (live 2026-10-21).
    [Fact]
    public void A_step_names_its_own_markers_shown_or_hidden_and_actors_by_sno()
    {
        const int Tharra = 1952922, Boss = 2420000;
        var statue = new PlacedMarker(1, Sphere, new(2719.3f, -24.8f, 5.8f));
        var forest = new PlacedMarker(1, Sphere, new(2498.8f, 29.9f, 15.7f));
        var step = new QuestPhaseObjects(12, [statue, forest], new HashSet<int> { Boss }, [], HiddenMarkers: new HashSet<PlacedMarker> { forest });

        Assert.Equal(StepPlace.Shown, QuestStep.Of(step, Sphere, new(2719.5f, -25f)));
        Assert.Equal(StepPlace.Hidden, QuestStep.Of(step, Sphere, new(2498.8f, 29.9f)));
        Assert.Equal(StepPlace.NotInStep, QuestStep.Of(step, Sphere, new(2650.8f, -46.6f)));
        Assert.Equal(StepPlace.NotInStep, QuestStep.Of(step, Tharra, new(2381.2f, -27.6f)));
        Assert.Equal(StepPlace.Shown, QuestStep.Of(step, Boss, new(0, 0)));
    }

    [Fact]
    public void With_no_quest_place_marked_it_says_so()
    {
        var rig = new Rig();
        rig.Bridge.OnMapMarkers([Markers[0]]);
        rig.Move(KedBardu);

        rig.Pad.Press();

        Assert.Equal(English.NoQuestPlace, rig.Speech.Texts.Last());
        Assert.Empty(rig.Searches);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void Outside_the_open_world_it_leaves_the_pad_alone()
    {
        var rig = new Rig("DGN_Step_Something");
        rig.Move(KedBardu);

        Assert.False(rig.Bridge.TakesPad);
        rig.Pad.Press();
        Assert.Empty(rig.Speech.Texts);
    }

    [Fact]
    public void Arriving_ends_it_with_the_chime()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();
        foreach (var p in Straight(KedBardu, LostPlains)) rig.Move(p);

        Assert.Equal(English.PathEnd, rig.Speech.Texts.Last());
        Assert.False(rig.Pings());
    }

    [Fact]
    public void Straying_from_the_path_finds_a_new_one_at_most_every_few_seconds()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();
        var astray = KedBardu + new Vector2(20, 30);
        rig.Move(astray);
        Assert.Single(rig.Searches); // the last search was a moment ago

        rig.Clock += QuestPathBridge.RepathInterval;
        rig.Move(astray);

        Assert.Equal(2, rig.Searches.Count);
        Assert.Equal(new Vector3(astray, 0), rig.Searches[1].From);
        rig.Move(astray);
        Assert.Equal(2, rig.Searches.Count); // on the new path
    }

    [Fact]
    public void Walking_beside_the_path_keeps_it_and_does_not_search_again()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();
        rig.Clock += QuestPathBridge.RepathInterval;

        // Five units beside the path the whole way: no point is ever within 4, yet the walk goes on to the end.
        var side = Vector2.Normalize(new Vector2(LostPlains.Y - KedBardu.Y, KedBardu.X - LostPlains.X)) * 5;
        foreach (var p in Straight(KedBardu, LostPlains)) rig.Move(p + side);
        rig.Move(LostPlains);

        Assert.Single(rig.Searches);
        Assert.Equal(English.PathEnd, rig.Speech.Texts.Last());
    }

    [Fact]
    public void With_no_path_it_says_so_and_points_straight_at_the_place()
    {
        var rig = new Rig { Path = (_, _) => null };
        rig.Move(KedBardu);
        rig.Pad.Press();

        Assert.Contains(English.PathNotFound, rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void A_torn_position_read_does_not_end_it()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();

        rig.Move(new Vector2(-431602080f, -431602080f));
        rig.Move(KedBardu + new Vector2(0, -1));

        Assert.True(rig.Pings()); // still leading: a stopped guide stays silent until started again
    }

    [Fact]
    public void A_teleport_away_or_a_world_change_ends_it()
    {
        var rig = new Rig();
        rig.Move(KedBardu);
        rig.Pad.Press();
        rig.Move(LostPlains + new Vector2(0, 2000));
        Assert.False(rig.Pings());

        rig.Move(KedBardu);
        rig.Pad.Press();
        rig.Feed.RaiseWorld("QST_Class_Warlock_DGN_01");
        Assert.False(rig.Pings());
        Assert.False(rig.Bridge.TakesPad);
    }

    // ---- fakes ----

    private sealed class FakeFeed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; }

        public void RaiseWorld(string name) => WorldChanged?.Invoke(1, name, false);
        public void RaisePlayer(Vector2 p) => PlayerMoved?.Invoke(new PlayerState(p.X, p.Y, 0, 0));
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
