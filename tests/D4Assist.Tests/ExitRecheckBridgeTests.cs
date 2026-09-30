using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Data.Readers;

namespace D4Assist.Tests;

public class ExitRecheckBridgeTests
{
    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld => null;
        public void RaiseWorld(string name, bool dungeon = true) => WorldChanged?.Invoke(0, name, dungeon);
        public void RaisePlayer() => PlayerMoved?.Invoke(new PlayerState(0, 0, 0, 0));
    }

    private sealed class Speech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private static GuideStep Recheck(int round, string id = "c") =>
        new(GuideStatus.Rechecking, new GuideTarget(GuideTargetKind.ExitCandidate, id, (5, 5)), (5, 5), 20, 0, round);

    private sealed class Rig
    {
        public Feed Feed = new();
        public Speech Speech = new();
        public GuideStep Step = Recheck(1);
        public bool Beacon;
        public List<bool> Asked = [];
        public List<string> Log = [];
        public List<GuideDestination> Destinations = [];
        public LanguageSetting Language = new();

        public Rig(string world = "X1_Undercity_Test")
        {
            var ping = new GuidePingPlayer(new AudioMixer(), () => 0, autoTick: false);
            _ = new ExplorationGuideBridge(
                Feed, (_, missing, _) => { Asked.Add(missing); return Step; }, ping, Speech, () => Beacon, Log.Add, language: Language,
                destinations: () => Destinations);
            Feed.RaiseWorld(world);
        }
    }

    [Fact]
    public void Speaks_once_per_round_without_interrupting_and_logs_round_and_target()
    {
        var rig = new Rig();
        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();
        rig.Step = Recheck(2, "d");
        rig.Feed.RaisePlayer();

        Assert.Equal(2, rig.Speech.Spoken.Count);
        Assert.All(rig.Speech.Spoken, s => Assert.False(s.Interrupt));
        Assert.Equal("Salida no encontrada. Revisando los callejones.", rig.Speech.Spoken[0].Text);
        Assert.Equal(2, rig.Log.Count(l => l.Contains("recheck round")));
        Assert.Equal(2, rig.Log.Count(l => l.Contains("GUIDE target ExitCandidate")));
    }

    [Fact]
    public void Speaks_english_when_the_language_is_english()
    {
        var rig = new Rig();
        rig.Language.Set(SpeechLanguage.English);
        rig.Feed.RaisePlayer();

        Assert.Equal("Exit not found. Rechecking dead ends.", rig.Speech.Spoken.Single().Text);
    }

    [Fact]
    public void Asks_for_a_recheck_in_endgame_worlds_only()
    {
        var endgame = new Rig("PIT_Test");
        endgame.Feed.RaisePlayer();
        var normal = new Rig("Scos_Cerrigar_Dungeon");
        normal.Feed.RaisePlayer();

        Assert.True(endgame.Asked.Single());
        Assert.Empty(normal.Asked); // guide is off in normal dungeons by default
    }

    [Fact]
    public void A_known_unused_portal_replaces_the_recheck()
    {
        var rig = new Rig("PIT_Test");
        rig.Destinations.Add(new GuideDestination("pad", (40, 0), 0));
        rig.Feed.RaisePlayer();

        Assert.False(rig.Asked.Single());
    }

    [Theory]
    [InlineData(0, "Todo explorado. Te llevo a un portal.")]
    [InlineData(1, "Todo explorado. Te llevo al portal por el que llegaste.")]
    public void Turning_to_a_portal_is_spoken_once(int tier, string expected)
    {
        var rig = new Rig("PIT_Test");
        rig.Destinations.Add(new GuideDestination("p", (40, 0), tier));
        rig.Step = new GuideStep(GuideStatus.Guiding, new GuideTarget(GuideTargetKind.Destination, "p", (40, 0)), (40, 0), 40);

        rig.Feed.RaisePlayer();
        rig.Feed.RaisePlayer();

        Assert.Equal(expected, rig.Speech.Spoken.Single().Text);
    }

    [Fact]
    public void Does_not_ask_or_speak_while_the_beacon_has_a_target()
    {
        var rig = new Rig { Beacon = true };
        rig.Feed.RaisePlayer();

        Assert.Empty(rig.Asked);
        Assert.Empty(rig.Speech.Spoken);
    }

    [Fact]
    public void Exit_candidates_come_from_both_layer_kinds_and_skip_unresolvable_scenes()
    {
        var scenes = new Dictionary<string, int> { ["Cave_Bug_E_01"] = 1, ["Sewers_Kurast_S_01"] = 2, ["Boss_Room"] = 3 };
        string[] names =
        [
            "Cave_Bug_E_01 (X1_Undercity_AreaExits)", "Sewers_Kurast_S_01 (X1_Undercity_AreaExits)",
            "Boss_Room (X1_Undercity_BossExits)", "Gone_Scene (X1_Undercity_AreaExits)",
            "Cave_Bug_E_01 (Other_Layer)", "Cave_Bug_E_01",
        ];

        var set = ExitCandidateScenes.Build(names, n => scenes.TryGetValue(n, out var sno) ? sno : throw new ArgumentException(n));

        Assert.Equal(new[] { 1, 2, 3 }, set.Order());
    }

    [Fact]
    public void Actor_log_lists_gizmos_and_portal_names_once_per_sno_and_again_after_reset()
    {
        var names = new Dictionary<int, string> { [1] = "X1_Undercity_WarpPad", [2] = "Rock", [3] = "Lever", [4] = "GateBeacon" };
        var defs = new Dictionary<int, ActorDefinitionInfo> { [1] = new(4, 57), [2] = new(4, -1), [3] = new(4, 100), [4] = new(4, -1) };
        var log = new List<string>();
        var sightings = new ActorSightingLog(sno => defs.TryGetValue(sno, out var d) ? d : null, sno => names.GetValueOrDefault(sno), log.Add);
        var player = new PlayerState(0, 0, 0, 0);
        ActorState[] actors = [new(10, 1, 3, 4, 0), new(11, 1, 9, 9, 0), new(12, 2, 1, 1, 0), new(13, 3, 0, 0, 0), new(14, 4, 0, 0, 0)];

        sightings.Note(actors, player);
        sightings.Note(actors, player);

        Assert.Equal(3, log.Count);
        Assert.Equal("ACTOR seen: X1_Undercity_WarpPad sno=1 gizmo=57 at (3.0,4.0) distance=5.0", log[0]);
        sightings.Reset();
        sightings.Note(actors, player);
        Assert.Equal(6, log.Count);
    }
}
