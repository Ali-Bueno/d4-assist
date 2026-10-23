using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Tests;

// Live 2026-10-23 (the Horadric guardian): its spirit bomb marks the player's spot with five effects at once
// (fxKit_damaging_persistentWarning_spirits_swirl_fxMesh x3, _volumeParent, _lightParent), 3-4 s before the burst.
public class TelegraphWarnerTests
{
    private const int GuardianSno = 1, SwirlSno = 2, LightSno = 3, PropSno = 4, PlayerWarningSno = 5;

    private static readonly Dictionary<int, string> Names = new()
    {
        [GuardianSno] = "QST_Horadric_Guardian_boss",
        [SwirlSno] = "fxKit_damaging_persistentWarning_spirits_swirl_fxMesh",
        [LightSno] = "fxKit_damaging_persistentWarning_spirits_lightParent",
        [PropSno] = "MageHalls_Prop_Scroll_01_Dyn",
        [PlayerWarningSno] = "Warlock_DemonicGaze_warning",
    };

    private sealed class FakeSpeech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private static readonly PlayerState Player = new(10, 10, 0, 1);

    private static ActorState Guardian(float x = 13, float y = 7) => new(100, GuardianSno, x, y, 0);

    private static ActorState Effect(uint id, int sno, float x, float y) => new(id, sno, x, y, 0, ClientOnly: true);

    private static (FakeFeed Feed, FakeSpeech Speech, List<double> Cues, Action<double> SetSeconds) Create(Func<float, float, bool>? blocked = null)
    {
        var feed = new FakeFeed();
        var speech = new FakeSpeech();
        var cues = new List<double>();
        var now = TimeSpan.Zero;
        _ = new TelegraphWarner(feed, new TelegraphClassifier(sno => Names.GetValueOrDefault(sno)),
            a => a.ActorSno == GuardianSno ? EnemyClass.Boss : null, speech, new LanguageSetting(SpeechLanguage.English), cues.Add, blocked,
            now: () => now);
        feed.RaisePlayer(Player);
        return (feed, speech, cues, s => now = TimeSpan.FromSeconds(s));
    }

    [Theory]
    [InlineData("fxKit_damaging_persistentWarning_spirits_swirl_fxMesh", TelegraphShape.Circle)]
    [InlineData("fxKit_damaging_coneWarning_demonic_motes", TelegraphShape.Cone)]
    [InlineData("fxKit_damaging_lineWarning_fire_center", TelegraphShape.Line)]
    [InlineData("MonsterAffix_Chaos_WarningRing", TelegraphShape.Ring)]
    [InlineData("camp_omath_boss_ProjWarning", TelegraphShape.Line)]
    [InlineData("Warlock_DemonicGaze_warning", null)]
    [InlineData("Kehj_RareSpawn_Zombie_Warning_Sign", null)]
    [InlineData("fxKit_damaging_burstCylindrical_spirits_face", null)]
    public void A_warning_is_told_by_name_with_its_shape(string name, TelegraphShape? shape) =>
        Assert.Equal(shape, TelegraphClassifier.Classify(name));

    [Fact]
    public void A_warning_under_the_player_in_a_boss_fight_says_which_way_to_step_once()
    {
        var (feed, speech, cues, _) = Create();
        feed.RaiseActors(Guardian(), Effect(1, PropSno, 12, 12));

        feed.RaiseActors(Guardian(), Effect(1, PropSno, 12, 12), Effect(2, SwirlSno, 10, 10), Effect(3, SwirlSno, 10, 10), Effect(4, LightSno, 10, 10));

        // The guardian stands left of the player (engine (+3,-3) from it is screen left): away from it is right.
        var expected = string.Format(AppStrings.English.DodgeFormat, NavigationPhrases.English.Direction(StickDirection.Right));
        Assert.Equal([(expected, true)], speech.Spoken);
        Assert.Single(cues);
    }

    [Fact]
    public void Outside_a_fight_or_far_off_nothing_is_said()
    {
        var (feed, speech, cues, _) = Create();
        feed.RaiseActors(Effect(2, SwirlSno, 10, 10)); // no boss near
        feed.RaiseActors(Guardian(), Effect(5, SwirlSno, 60, 60)); // a warning far away
        feed.RaiseActors(Guardian(), Effect(6, SwirlSno, 18, 10)); // near, but the player stands clear of it
        feed.RaiseActors(Guardian(), Effect(7, PlayerWarningSno, 10, 10)); // the player's own power

        Assert.Empty(speech.Spoken);
        Assert.Empty(cues);
    }

    [Fact]
    public void Warnings_close_together_ring_each_but_speak_at_most_once_a_gap()
    {
        var (feed, speech, cues, setSeconds) = Create();
        feed.RaiseActors(Guardian(), Effect(2, SwirlSno, 10, 10));
        setSeconds(0.5);
        feed.RaiseActors(Guardian(), Effect(3, SwirlSno, 13.5f, 10)); // another spot, half a second later
        setSeconds(2);
        feed.RaiseActors(Guardian(), Effect(4, SwirlSno, 7, 10));

        Assert.Equal(3, cues.Count);
        Assert.Equal(2, speech.Spoken.Count);
    }

    [Fact]
    public void A_world_change_forgets_the_player()
    {
        var (feed, speech, _, _) = Create();
        feed.RaiseWorld();
        feed.RaiseActors(Guardian(), Effect(2, SwirlSno, 10, 10));

        Assert.Empty(speech.Spoken);
    }
}
