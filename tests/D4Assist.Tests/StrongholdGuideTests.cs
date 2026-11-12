using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Data.Readers;

namespace D4Assist.Tests;

// The Altar of Ruin (2026-11-11): "Explore the cave" (phase 0) waits on a keystone of the cave's high priest, whom only the next
// phase ("Kill the high priests") names; phase 0's hidden trigger is its other way out.
public class StrongholdGuideTests
{
    private const string World = "Sanctuary_Eastern_Continent";
    private const int Quest = 472774, TriggerSno = 298374, PriestSno = 472180, Priest3Sno = 472159, CultistSno = 542514, ProxySno = 9;
    private const uint ExplorePhase = 0, KillPhase = 12, LastPhase = 109;
    private static readonly Vector2 Altar = new(-38, -720), Trigger = new(-32, -722), Priest = new(-35, -744), Priest3 = new(-32, -697);
    private static readonly Stronghold AltarOfRuin = new(Quest, "CMP_Kehj_AltarOfRuin", 1, new Vector3(Altar, 0));

    private static PlacedMarker At(int sno, Vector2 p) => new(1, sno, new Vector3(p, 0));

    private static readonly IReadOnlyList<QuestPhaseObjects> Phases =
    [
        new(ExplorePhase, [At(TriggerSno, Trigger)], new HashSet<int>(), [], HiddenMarkers: new HashSet<PlacedMarker> { At(TriggerSno, Trigger) }),
        new(KillPhase, [At(PriestSno, Priest)], new HashSet<int>(), []),
        new(LastPhase, [At(Priest3Sno, Priest3)], new HashSet<int> { PriestSno }, [], HiddenMarkers: new HashSet<PlacedMarker> { At(Priest3Sno, Priest3) }),
    ];

    private static readonly MarkedActor[] Map =
    [
        new(TriggerSno, Trigger.X, Trigger.Y, 0, 0, 11),
        new(PriestSno, Priest.X, Priest.Y, 0, 0, 10),
        new(Priest3Sno, Priest3.X, Priest3.Y, 0, 0, 10),
        new(CultistSno, -9, -734, 0, 0, 10),
        new(CultistSno, 3000, 3000, 0, 0, 10), // far outside
    ];

    private static StrongholdTargets? Targets(Vector2 here, bool explore, uint? phase, IReadOnlyList<MarkedActor>? map = null, int[]? running = null) =>
        StrongholdTargeting.At(here, map ?? Map, explore, [AltarOfRuin], running ?? [Quest], _ => phase, _ => Phases, _ => "Altar of Ruin",
            m => m.ActorSno == PriestSno ? "Cultist High Priestess" : null, m => m.ActorSno == ProxySno);

    private static List<StrongholdTarget> Ranked(StrongholdTargets t, Vector2 here) =>
        [.. t.Targets.OrderBy(x => x.Tier).ThenBy(x => Vector2.Distance(here, x.Place.Position))];

    [Fact]
    public void A_step_naming_no_place_leads_to_the_next_steps_before_its_own_hidden_trigger()
    {
        var targets = Targets(Altar, explore: false, ExplorePhase)!;

        var ranked = Ranked(targets, Altar);
        Assert.Equal(PriestSno, ranked[0].Place.ActorSno);
        Assert.Equal("Cultist High Priestess", ranked[0].Place.QuestName);
        Assert.Equal(TriggerSno, ranked[1].Place.ActorSno);
        Assert.Equal("Altar of Ruin", ranked[1].Place.QuestName);
        Assert.True(ranked[0].Tier < ranked[1].Tier);
        Assert.DoesNotContain(targets.Targets, t => t.Place.ActorSno == CultistSno); // no step names it
    }

    [Fact]
    public void A_monster_a_kill_step_names_only_hidden_counts_as_the_steps_own()
    {
        var targets = Targets(Altar, explore: false, LastPhase)!;

        Assert.All(targets.Targets.Where(t => t.Place.ActorSno is PriestSno or Priest3Sno), t => Assert.Equal(StrongholdTargeting.ShownTier, t.Tier));
    }

    [Fact]
    public void Exploring_takes_every_named_place_and_marked_monster_in_the_stronghold()
    {
        var targets = Targets(Altar, explore: true, ExplorePhase)!;

        Assert.Equal(4, targets.Targets.Count); // trigger, two priests, the cultist; not the one far away
        Assert.All(targets.Targets, t => Assert.Equal(0, t.Tier));
    }

    [Fact]
    public void Outside_a_stronghold_or_once_it_is_freed_there_are_none()
    {
        Assert.Null(Targets(Altar + new Vector2(500, 0), explore: false, ExplorePhase));
        Assert.Null(Targets(Altar, explore: false, ExplorePhase, running: []));
    }

    [Fact]
    public void The_games_own_quest_marker_is_a_step_place()
    {
        var targets = Targets(Altar, explore: false, ExplorePhase, [.. Map, new(ProxySno, -20, -730, 0, 0, 12)])!;

        Assert.Contains(targets.Targets, t => t.Place.ActorSno == ProxySno && t.Tier == StrongholdTargeting.ShownTier);
    }

    // The guide itself: D-pad left on and off, no list; reached targets chime, hold, and lead on.
    private sealed class Rig
    {
        public readonly FakeFeed Feed = new();
        public readonly FakeSpeech Speech = new();
        public readonly FakePad Pad = new();
        public readonly AudioMixer Mixer = new();
        public readonly QuestPathBridge Bridge;
        public TimeSpan Clock;
        public uint Phase = ExplorePhase;
        public List<MarkedActor> MapMarks = [.. Map];
        public int[] Running = [Quest];
        public List<Vector2> SearchedTo = [];
        public bool? Steering = true;

        public Rig()
        {
            var ping = new GuidePingPlayer(Mixer, () => Clock.TotalSeconds, autoTick: false);
            Bridge = new QuestPathBridge(Feed, w => w == World, _ => [], (_, _, to) => { SearchedTo.Add(to); return [to]; }, ping, Speech,
                new LanguageSetting(SpeechLanguage.English), Pad, runInBackground: work => work(), clock: () => Clock,
                strongholdTargets: (marks, here, explore) => StrongholdTargeting.At(here, marks, explore, [AltarOfRuin], Running, _ => Phase,
                    _ => Phases, _ => "Altar of Ruin", m => m.ActorSno == PriestSno ? "Cultist High Priestess" : null, _ => false),
                steering: () => Steering);
            Feed.RaiseWorld(World);
            Read();
        }

        public void Read() => Bridge.OnMapMarkers([.. MapMarks]);
        public void Move(Vector2 p) { Clock += TimeSpan.FromSeconds(0.1); Feed.RaisePlayer(p); }
        public void Wait(TimeSpan time) { Clock += time; Read(); }
        public Vector2 Target => SearchedTo[^1];
    }

    [Fact]
    public void D_pad_left_in_a_stronghold_switches_the_guide_on_to_the_steps_target_and_off_again()
    {
        var rig = new Rig();
        rig.Move(Altar);

        rig.Pad.Press();

        Assert.Equal(Priest, rig.Target);
        Assert.Equal(AppStrings.English.GuideOn, rig.Speech.Texts.First());
        Assert.Contains(rig.Speech.Texts, t => t.Contains("Cultist High Priestess"));
        Assert.DoesNotContain(rig.Speech.Texts, t => t.Contains(" of ")); // no list count
        int searches = rig.SearchedTo.Count;

        rig.Pad.Press();

        Assert.Equal(AppStrings.English.GuideOff, rig.Speech.Texts.Last());
        rig.Read();
        Assert.Equal(searches, rig.SearchedTo.Count);
    }

    [Fact]
    public void A_target_reached_holds_and_the_guide_leads_on_by_itself_once_the_player_steps_away()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Pad.Press();

        rig.Move(Priest + new Vector2(1, 0)); // reached
        rig.Read();
        Assert.Equal(Priest, rig.Target);     // holds there

        rig.Move(Priest + new Vector2(QuestPathBridge.HoldUnits + 2, 0));

        Assert.Equal(Trigger, rig.Target);    // the next target, no press
    }

    [Fact]
    public void A_monster_target_the_map_stops_marking_gives_way_to_the_next()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Pad.Press();

        rig.MapMarks.RemoveAll(m => m.ActorSno == PriestSno); // killed
        rig.Read();
        rig.Wait(QuestPathBridge.GoneAfter);

        Assert.Equal(Trigger, rig.Target);
    }

    [Fact]
    public void The_quest_moving_on_leads_to_the_new_steps_target()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Pad.Press();
        rig.Move(Priest + new Vector2(1, 0));
        rig.Read();

        rig.Phase = LastPhase;
        rig.MapMarks.RemoveAll(m => m.ActorSno == PriestSno);
        rig.Read();

        Assert.Equal(Priest3, rig.Target);
    }

    // Live 2026-11-11: "Kill the high priests" named only the cave's, killed already; the guide said "no target".
    [Fact]
    public void A_step_with_no_target_left_explores_by_itself()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Phase = KillPhase;
        rig.MapMarks.RemoveAll(m => m.ActorSno is PriestSno or Priest3Sno); // this step's and the next one's, killed
        rig.Read();

        rig.Pad.Press();

        Assert.Equal(Trigger, rig.Target); // the nearest place the quest names anywhere
        Assert.DoesNotContain(AppStrings.English.NoQuestPlace, rig.Speech.Texts);
    }

    [Fact]
    public void Freeing_the_stronghold_switches_the_guide_off()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Pad.Press();

        rig.Running = [];
        rig.Read();

        Assert.Equal(AppStrings.English.GuideOff, rig.Speech.Texts.Last());
    }

    [Fact]
    public void Holding_d_pad_left_switches_to_exploring_and_back()
    {
        var rig = new Rig();
        rig.Move(Altar);
        rig.Pad.Press();

        rig.Pad.Hold();
        Assert.Contains(AppStrings.English.GuideExploreMode, rig.Speech.Texts);
        Assert.Equal(Trigger, rig.Target); // the nearest of all (6 units): exploring takes every marked place alike

        rig.Pad.Hold();
        Assert.Contains(AppStrings.English.GuideObjectivesMode, rig.Speech.Texts);
        Assert.Equal(Priest, rig.Target);
    }

    // Live 2026-11-12, Kor Dragan: chosen in the map list, the path to its spot went on inside, to a shut gate.
    [Fact]
    public void A_stronghold_chosen_in_the_map_list_hands_over_to_its_guide_once_entered()
    {
        var rig = new Rig();
        rig.Move(Altar + new Vector2(300, 0));
        rig.Bridge.Choose(World, "Altar of Ruin", Altar);
        rig.Pad.Press();
        Assert.Equal(Altar, rig.Target); // outside: the way to the place chosen

        rig.Move(Altar + new Vector2(StrongholdTargeting.StrongholdUnits - 20, 0));
        rig.Read();

        Assert.Equal(Priest, rig.Target); // inside: the step's target, no press

        rig.Pad.Press();
        Assert.Equal(AppStrings.English.GuideOff, rig.Speech.Texts.Last());
        rig.Pad.Press();
        Assert.Equal(Priest, rig.Target); // the place chosen is done with: the stronghold guide again
        Assert.Equal(AppStrings.English.GuideOn, rig.Speech.Texts[^2]);
    }

    [Fact]
    public void A_place_chosen_elsewhere_keeps_leading_through_a_stronghold()
    {
        var rig = new Rig();
        var cellar = Altar + new Vector2(400, 0);
        rig.Move(Altar);
        rig.Bridge.Choose(World, "Cellar", cellar);
        rig.Pad.Press();
        rig.Read();

        Assert.Equal(cellar, rig.Target);
    }

    // Live 2026-11-12, Kor Dragan: the player stood still (at a portal, reading), and every eight seconds the way ahead was
    // blocked and another found, until none was left.
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void A_player_who_does_not_steer_is_not_stuck(bool? steering)
    {
        var rig = new Rig { Steering = steering };
        rig.Move(Altar + new Vector2(60, 0));
        rig.Pad.Press();
        int searches = rig.SearchedTo.Count;

        for (int i = 0; i < 300; i++) rig.Move(Altar + new Vector2(60, 0)); // 30 s on one spot

        Assert.Equal(searches, rig.SearchedTo.Count);
        Assert.DoesNotContain(PathStrings.English.Blocked, rig.Speech.Texts);
    }

    [Fact]
    public void A_player_who_steers_and_gets_nowhere_is_stuck()
    {
        var rig = new Rig();
        rig.Move(Altar + new Vector2(60, 0));
        rig.Pad.Press();

        for (int i = 0; i < 300; i++) rig.Move(Altar + new Vector2(60, 0));

        Assert.Contains(PathStrings.English.Blocked, rig.Speech.Texts);
    }

    [Theory]
    [InlineData(new byte[] { 0x01, 128, 128, 0, 0, 0, 0, 0, 8 }, false, false)] // USB, at rest
    [InlineData(new byte[] { 0x01, 200, 128, 0, 0, 0, 0, 0, 8 }, false, true)]  // USB, pushed right
    [InlineData(new byte[] { 0x01, 128, 30, 0, 0, 8 }, true, true)]             // Bluetooth short report, pushed up
    [InlineData(new byte[] { 0x31, 0, 128, 128, 0, 0, 0, 0, 0, 8 }, true, false)] // Bluetooth full report, at rest
    public void A_dualsense_report_tells_whether_the_left_stick_is_pushed(byte[] report, bool bluetooth, bool pushed) =>
        Assert.Equal(pushed, SonyPadReport.IsLeftStickPushed(report, SonyPadModel.DualSense, bluetooth));

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
        public List<string> Texts { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Texts.Add(text);
        public void Dispose() { }
    }

    private sealed class FakePad : IGuideTogglePad
    {
        public event Action? DpadLeftPressed;
        public event Action? DpadLeftHeld;
        public void Press() => DpadLeftPressed?.Invoke();
        public void Hold() => DpadLeftHeld?.Invoke();
    }
}
