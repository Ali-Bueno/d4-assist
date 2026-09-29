using D4Assist.Audio;
using D4Assist.Speech;

namespace D4Assist;

/// <summary>
/// One sound example per Ctrl+Alt+Shift+digit, so the cues can be shown (e.g. in a video) without a fight's noise
/// (user request 2026-09-29): its name is spoken, then radar cues play once per direction, the quest loop twice,
/// the guide ping three times, then the arrival chime. One example at a time; presses during one are ignored.
/// </summary>
/// <param name="unmute">Lifts the cue pause (game in the background, cutscene) while an example plays; null when none.</param>
public sealed class SoundDemo(
    ISpeechSink speech, Func<int, string> nameOf, IRadarCuePlayer? radar, AudioMixer mixer, float[]? questLoop,
    double questGain, GuidePingPlayer? guide, Func<IDisposable>? unmute = null)
{
    public const int ExampleCount = 8;

    // Same pacing as the --sound-test walkthrough (SoundTestMode.PauseMs).
    private const int GapMs = 400;
    private const int QuestRepeats = 2, GuidePings = 3;

    private static readonly RadarRay[] Rays = [RadarRay.Up, RadarRay.Right, RadarRay.Down, RadarRay.Left];

    private readonly SemaphoreSlim _busy = new(1, 1);

    /// <summary>Starts example <paramref name="digit"/> (1-8) in the background; ignored while another is playing.</summary>
    public void Play(int digit)
    {
        if (digit is < 1 or > ExampleCount || !_busy.Wait(0)) return;
        Task.Run(() =>
        {
            try { using (unmute?.Invoke()) Run(digit); }
            finally { _busy.Release(); }
        });
    }

    /// <summary>Runs example <paramref name="digit"/> on the calling thread (tests call this directly).</summary>
    public void Run(int digit)
    {
        speech.Speak(nameOf(digit), interrupt: true);
        Thread.Sleep(GapMs);
        switch (digit)
        {
            case 1: Radar(RadarCueKind.Exit, unexplored: false); break;
            case 2: Radar(RadarCueKind.Open, unexplored: false); break;
            case 3: Radar(RadarCueKind.Blocked, unexplored: false); break;
            case 4: Radar(RadarCueKind.Door, unexplored: false); break;
            case 5: Radar(RadarCueKind.Exit, unexplored: true); break;
            case 6: Repeat(QuestRepeats, () => { if (questLoop is not null) mixer.Play(questLoop, questGain, pan: 0); }, questLoop is null ? 0 : questLoop.Length * 1000 / AudioFormat.SampleRate); break;
            case 7: Repeat(GuidePings, () => guide?.PingOnce(), 0); break;
            case 8: guide?.PlayArrived(); break;
        }
    }

    private void Radar(RadarCueKind kind, bool unexplored)
    {
        if (radar is null) return;
        foreach (var ray in Rays)
        {
            radar.Play(kind, ray, unexplored);
            Thread.Sleep(GapMs);
        }
    }

    private static void Repeat(int times, Action play, int soundMs)
    {
        for (int i = 0; i < times; i++)
        {
            play();
            Thread.Sleep(soundMs + GapMs);
        }
    }
}
