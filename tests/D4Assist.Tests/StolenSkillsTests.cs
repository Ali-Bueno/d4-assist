using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Tests;

/// <summary>StolenSkillsAnnouncer: Mephisto's skill orbs appearing and all being taken back.</summary>
public class StolenSkillsTests
{
    private const int OrbSno = 1, OtherSno = 2;

    private sealed class FakeSpeech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private static readonly AppStrings Strings = AppStrings.Spanish;

    private static (FakeFeed Feed, FakeSpeech Speech, Action<double> SetSeconds) Create()
    {
        var feed = new FakeFeed();
        var speech = new FakeSpeech();
        var now = TimeSpan.Zero;
        _ = new StolenSkillsAnnouncer(feed, sno => sno == OrbSno, speech, new LanguageSetting(SpeechLanguage.Spanish), () => now);
        return (feed, speech, s => now = TimeSpan.FromSeconds(s));
    }

    private static ActorState Orb(uint id) => new(id, OrbSno, 0, 0, 0);

    [Fact]
    public void The_first_orb_announces_the_theft_once_interrupting()
    {
        var (feed, speech, _) = Create();

        feed.RaiseActors(new ActorState(9, OtherSno, 0, 0, 0));
        feed.RaiseActors(Orb(1), Orb(2));
        feed.RaiseActors(Orb(1), Orb(2), Orb(3));

        Assert.Equal([(Strings.SkillsStolen, true)], speech.Spoken);
    }

    [Fact]
    public void Skills_count_as_back_only_once_no_orb_was_seen_for_a_while()
    {
        var (feed, speech, at) = Create();
        feed.RaiseActors(Orb(1));

        at(1);
        feed.RaiseActors(); // one poll without the orb: maybe a glitch
        Assert.Single(speech.Spoken);

        at(1 + StolenSkillsAnnouncer.ClearAfter.TotalSeconds);
        feed.RaiseActors();

        Assert.Equal((Strings.SkillsRecovered, false), speech.Spoken[^1]);
    }

    [Fact]
    public void Leaving_the_world_forgets_the_theft_silently()
    {
        var (feed, speech, at) = Create();
        feed.RaiseActors(Orb(1));

        feed.RaiseWorld();
        at(10);
        feed.RaiseActors();

        Assert.Single(speech.Spoken);
    }
}
