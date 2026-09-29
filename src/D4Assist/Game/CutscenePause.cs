using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Silences every cue while a game cutscene plays (CLAUDE.md §9). FenrisDebug.txt logs only its start
/// (research/runtime-signals.md §2), so the pause ends when the world changes or the player walks out of the map cell they stood in.
/// </summary>
public sealed class CutscenePause
{
    /// <summary>The logged cutscene state that means one is on screen.</summary>
    public const string PlaybackState = "PLAYBACK";

    // One walk-map cell: a scripted camera leaves the player in place, walking again means control is back.
    private const double ResumeDistance = NavGridReader.MapResolution;

    private readonly Action<bool> _setPaused;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private PlayerState? _anchor;

    /// <param name="setPaused">Applies the pause, e.g. to the shared AudioMixer.</param>
    /// <param name="log">Debug sink for cutscene lines and pause changes; null when not debugging.</param>
    public CutscenePause(Action<bool> setPaused, Action<string>? log = null)
    {
        _setPaused = setPaused;
        _log = log;
    }

    public bool IsPaused { get; private set; }

    public void OnCutsceneState(string state)
    {
        lock (_gate)
        {
            _log?.Invoke($"CUTSCENE state {state}");
            if (state != PlaybackState || IsPaused) return;
            _anchor = null;
            SetPaused(true, "cutscene");
        }
    }

    public void OnWorldChanged() { lock (_gate) if (IsPaused) SetPaused(false, "world changed"); }

    public void OnPlayer(PlayerState p)
    {
        lock (_gate)
        {
            if (!IsPaused || p.IsWorldUnloaded) return;
            _anchor ??= p;
            double dx = p.X - _anchor.X, dy = p.Y - _anchor.Y;
            if (dx * dx + dy * dy >= ResumeDistance * ResumeDistance) SetPaused(false, "player walked");
        }
    }

    private void SetPaused(bool paused, string cause)
    {
        IsPaused = paused;
        _setPaused(paused);
        _log?.Invoke($"CUES {(paused ? "paused" : "resumed")} ({cause})");
    }
}
