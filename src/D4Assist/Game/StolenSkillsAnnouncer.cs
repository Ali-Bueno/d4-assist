using System.Diagnostics;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>Says when Mephisto takes the player's skills (his skill orbs appear) and when every orb has been taken back
/// (research/dungeon-objectives.md §11).</summary>
public sealed class StolenSkillsAnnouncer
{
    /// <summary>An orb missing from a poll or two is no pickup: the skills count as back only after this long without one.</summary>
    public static readonly TimeSpan ClearAfter = TimeSpan.FromSeconds(2);

    private readonly Func<int, bool> _isOrb;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Func<TimeSpan> _now;
    private readonly object _gate = new();
    private bool _stolen;
    private TimeSpan _lastOrb;

    /// <param name="isOrb">True for a stolen-skill orb's actor SNO (ObjectiveClassifier.IsStolenSkill).</param>
    /// <param name="now">Elapsed-time clock; null uses a stopwatch.</param>
    public StolenSkillsAnnouncer(IDungeonFeed feed, Func<int, bool> isOrb, ISpeechSink speech, LanguageSetting language, Func<TimeSpan>? now = null)
    {
        _isOrb = isOrb;
        _speech = speech;
        _language = language;
        var clock = Stopwatch.StartNew();
        _now = now ?? (() => clock.Elapsed);

        feed.WorldChanged += (_, _, _) => { lock (_gate) _stolen = false; };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        var now = _now();
        if (actors.Any(a => _isOrb(a.ActorSno)))
        {
            _lastOrb = now;
            if (_stolen) return;
            _stolen = true;
            _speech.Speak(AppStrings.For(_language.Current).SkillsStolen, interrupt: true);
        }
        else if (_stolen && now - _lastOrb >= ClearAfter)
        {
            _stolen = false;
            _speech.Speak(AppStrings.For(_language.Current).SkillsRecovered, interrupt: false);
        }
    }
}
