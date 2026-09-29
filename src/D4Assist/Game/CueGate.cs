namespace D4Assist.Game;

/// <summary>Why the cues are silent right now.</summary>
public enum CueMute { Cutscene, GameInBackground, Conversation }

/// <summary>
/// The one switch for the shared mixer's pause (CLAUDE.md §9): silent while any <see cref="CueMute"/> holds,
/// except while a sound example plays (the examples work outside the game too).
/// </summary>
public sealed class CueGate(Action<bool> setPaused, Action<string>? log = null)
{
    private readonly HashSet<CueMute> _reasons = [];
    private readonly object _gate = new();
    private int _examples;

    public bool IsPaused { get { lock (_gate) return Paused; } }

    public void Set(CueMute reason, bool on)
    {
        lock (_gate)
        {
            if (!(on ? _reasons.Add(reason) : _reasons.Remove(reason))) return;
            log?.Invoke($"CUES {(on ? "muted" : "unmuted")}: {reason}");
            Apply();
        }
    }

    /// <summary>Lifts the pause until the returned handle is disposed.</summary>
    public IDisposable Example()
    {
        lock (_gate) { _examples++; Apply(); }
        return new Release(this);
    }

    private bool Paused => _reasons.Count > 0 && _examples == 0;

    private void Apply() => setPaused(Paused);

    private sealed class Release(CueGate owner) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 1) return;
            lock (owner._gate) { owner._examples--; owner.Apply(); }
        }
    }
}
