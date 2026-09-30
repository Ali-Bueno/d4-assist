namespace D4Assist.Game;

/// <summary>Mutes the cues while any watched game menu (inventory, skills, map, pause) is open.</summary>
public sealed class MenuMute(Action<bool> setMuted, Action<string>? log = null)
{
    private readonly object _gate = new();
    private IReadOnlyList<string>? _last;

    public bool IsMuted { get; private set; }

    public void OnScreens(IReadOnlyList<string> open)
    {
        lock (_gate)
        {
            if (_last is { } last && last.SequenceEqual(open)) return;
            _last = open;
            log?.Invoke(open.Count > 0 ? $"MENU open: {string.Join(", ", open)}" : "MENU closed");
            Set(open.Count > 0);
        }
    }

    /// <summary>Forgets the state and unmutes; call on world change or when the session ends.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _last = null;
            Set(false);
        }
    }

    private void Set(bool muted)
    {
        if (muted == IsMuted) return;
        IsMuted = muted;
        setMuted(muted);
    }
}
