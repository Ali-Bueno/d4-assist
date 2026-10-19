using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class QuestPathTests
{
    private const string World = "Sanctuary_Eastern_Continent";
    private const int Quest = 2449612, Sphere = 9001, OtherActor = 9002;
    private static readonly Vector2 Target = new(-1134f, -1095f);
    private static readonly Vector2 Farobru = new(-1185f, -884f);
    private static readonly AppStrings English = AppStrings.English;

    private static readonly LoadedQuestDestination Destination =
        new(QuestDestinationCatalog.WarlockDisciplesStart, Quest, Sphere, Target);

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
        // The path the search returns: straight south from the player to the target in 5-unit steps, unless set.
        public Func<Vector3, Vector2, IReadOnlyList<Vector2>?> Path = (from, to) => Straight(new(from.X, from.Y), to);

        public Rig()
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            Bridge = new QuestPathBridge(Feed, [Destination], (world, from, to) => { Searches.Add((world, from, to)); return Path(from, to); },
                Ping, Speech, language: new LanguageSetting(SpeechLanguage.English), pad: Pad, runInBackground: work => work(), clock: () => Clock);
            Feed.RaiseWorld(World);
        }

        private Vector2 _last;

        public void Move(Vector2 p) { Now += 0.1; Clock += TimeSpan.FromSeconds(0.1); _last = p; Feed.RaisePlayer(p); }

        public void StepNamesTarget() => Bridge.OnQuestPlaces([new QuestPlace(Sphere, Target.X + 1, Target.Y, 0)]);

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
    public void The_quest_step_naming_the_place_starts_the_guide_along_a_found_path()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        Assert.False(rig.Pings());

        rig.StepNamesTarget();

        Assert.Contains(English.PathGuide, rig.Speech.Texts);
        var search = Assert.Single(rig.Searches);
        Assert.Equal((World, new Vector3(Farobru, 0), Target), search);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void Another_place_or_a_far_player_does_not_start_it()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.Bridge.OnQuestPlaces([new QuestPlace(OtherActor, Target.X, Target.Y, 0), new QuestPlace(Sphere, Target.X + 50, Target.Y, 0)]);
        rig.Move(Target + new Vector2(0, 700));
        rig.StepNamesTarget();

        Assert.Empty(rig.Searches);
        Assert.Empty(rig.Speech.Texts);
        Assert.False(rig.Pings());
    }

    [Fact]
    public void D_pad_left_starts_it_near_the_place_and_nowhere_else()
    {
        var rig = new Rig();
        rig.Move(Target + new Vector2(0, 700));
        Assert.False(rig.Bridge.TakesPad);
        rig.Pad.Press();
        Assert.Empty(rig.Searches);

        rig.Move(Farobru);
        Assert.True(rig.Bridge.TakesPad);
        rig.Pad.Press();

        Assert.Single(rig.Searches);
        Assert.Contains(English.PathGuide, rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void Arriving_ends_it_with_the_chime_and_the_step_does_not_restart_it_in_this_visit()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();
        foreach (var p in Straight(Farobru, Target)) rig.Move(p);

        Assert.Contains(English.PathEnd, rig.Speech.Texts);
        Assert.False(rig.Pings());
        rig.StepNamesTarget();
        Assert.Single(rig.Searches);
    }

    [Fact]
    public void Straying_from_the_path_finds_a_new_one_at_most_every_few_seconds()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();
        var astray = Farobru + new Vector2(40, -20);
        rig.Move(astray);
        Assert.Single(rig.Searches); // the last search was a moment ago

        rig.Clock += QuestPathBridge.RepathInterval;
        rig.Move(astray);

        Assert.Equal(2, rig.Searches.Count);
        Assert.Equal(new Vector3(astray, 0), rig.Searches[1].From);
        rig.Move(astray);
        Assert.Equal(2, rig.Searches.Count); // back on the new path
    }

    [Fact]
    public void With_no_path_it_says_so_and_points_straight_at_the_place()
    {
        var rig = new Rig { Path = (_, _) => null };
        rig.Move(Farobru);
        rig.StepNamesTarget();

        Assert.Contains(English.PathNotFound, rig.Speech.Texts);
        Assert.True(rig.Pings());
    }

    [Fact]
    public void A_teleport_away_or_a_world_change_ends_it()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();
        rig.Move(Target + new Vector2(0, 1000));
        Assert.False(rig.Pings());

        rig.Move(Farobru);
        rig.Pad.Press();
        rig.Feed.RaiseWorld("QST_Class_Warlock_DGN_01");
        Assert.False(rig.Pings());
        Assert.False(rig.Bridge.TakesPad);
    }

    [Fact]
    public void A_torn_position_read_does_not_end_it()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();

        rig.Move(new Vector2(-431602080f, -431602080f));
        rig.Move(Farobru + new Vector2(0, -1));

        Assert.True(rig.Pings()); // still leading: a stopped guide stays silent until started again
    }

    [Fact]
    public void Walking_beside_the_path_keeps_it_and_does_not_search_again()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();
        rig.Clock += QuestPathBridge.RepathInterval;

        // Five units beside the path the whole way: no point is ever within 4, yet the walk goes on to the end.
        var side = Vector2.Normalize(new Vector2(Target.Y - Farobru.Y, Farobru.X - Target.X)) * 5;
        foreach (var p in Straight(Farobru, Target)) rig.Move(p + side);
        rig.Move(Target);

        Assert.Single(rig.Searches);
        Assert.Contains(English.PathEnd, rig.Speech.Texts);
    }

    [Fact]
    public void D_pad_left_mutes_and_unmutes_it_while_it_leads()
    {
        var rig = new Rig();
        rig.Move(Farobru);
        rig.StepNamesTarget();

        rig.Pad.Press();
        Assert.False(rig.Pings());
        Assert.Contains(English.GuideOff, rig.Speech.Texts);
        rig.Pad.Press();
        Assert.True(rig.Pings());
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
