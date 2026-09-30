using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Mutes the cues while any watched game menu (inventory, skills, map, pause) is open, and for a moment after the NPC
/// interaction menu closes: a scene picked there can open with music before its conversation shows up (1.7 s, live
/// 2026-09-30, Neyrelle's "I'll protect you"; research/runtime-signals.md §2). The conversation mute takes over from there.
/// </summary>
/// <param name="clock">UTC now; tests pass their own.</param>
public sealed class MenuMute(Action<bool> setMuted, Action<string>? log = null, Func<DateTime>? clock = null)
{
    /// <summary>How long the cues stay muted after the NPC interaction menu closes.</summary>
    public static readonly TimeSpan NpcMenuHold = TimeSpan.FromSeconds(3);

    private readonly object _gate = new();
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);
    private IReadOnlyList<string>? _last;
    private DateTime _holdUntil = DateTime.MinValue;
    private bool _muted;

    public bool AnyOpen { get; private set; }

    public void OnScreens(IReadOnlyList<string> open)
    {
        lock (_gate)
        {
            if (_last is not { } last || !last.SequenceEqual(open))
            {
                if (_last is { } previous && previous.Contains(MenuScreens.NpcInteraction) && !open.Contains(MenuScreens.NpcInteraction))
                    _holdUntil = _now() + NpcMenuHold;
                _last = open;
                AnyOpen = open.Count > 0;
                log?.Invoke(AnyOpen ? $"MENU open: {string.Join(", ", open)}" : "MENU closed");
            }
            Set(AnyOpen || _now() < _holdUntil);
        }
    }

    /// <summary>Forgets the state and unmutes; call on world change or when the session ends.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _last = null;
            _holdUntil = DateTime.MinValue;
            AnyOpen = false;
            Set(false);
        }
    }

    private void Set(bool muted)
    {
        if (muted == _muted) return;
        _muted = muted;
        setMuted(muted);
    }
}
