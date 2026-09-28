namespace D4Assist.Navigation;

/// <summary>
/// Gates room-change announcements. Ported line-for-line from D2R's
/// `Rooms::Hysteresis` (rooms.h:42-54, rooms.cpp `Observe`): a room flip must hold
/// <see cref="DwellMs"/> unless it's the first room, or it's entered more than
/// <see cref="SettleMs"/> after the last change (both then speak immediately).
/// Values unchanged from D2R: this gates room-ENTRY debounce (a UI/announcement
/// timing choice), not radar cue range — the only thing the narrower D4 tile window
/// affects (research/d2r-radar-port.md §5, "Main gap").
/// </summary>
public sealed class RoomHysteresis
{
    public const int SettleMs = 1000; // rooms.h:48
    public const int DwellMs = 400;   // rooms.h:48

    private RoomKey _current = RoomKey.Invalid;
    private RoomKey _candidate = RoomKey.Invalid;
    private long _enteredAtMs = -1;
    private long _candidateSinceMs = -1;

    /// <summary>True when <paramref name="observed"/> becomes the current room and should be announced.</summary>
    public bool Observe(RoomKey observed, long nowMs)
    {
        if (observed == _current || !observed.IsValid)
        {
            DropCandidate();
            return false;
        }

        var settled = !_current.IsValid || nowMs - _enteredAtMs >= SettleMs;
        if (!settled)
        {
            if (observed != _candidate)
            {
                _candidate = observed;
                _candidateSinceMs = nowMs;
            }
            if (nowMs - _candidateSinceMs < DwellMs) return false;
        }

        _current = observed;
        DropCandidate();
        _enteredAtMs = nowMs;
        return true;
    }

    public void Reset()
    {
        _current = RoomKey.Invalid;
        DropCandidate();
        _enteredAtMs = -1;
    }

    /// <summary>Replaces the current room's key in place (a room that grew a new member tile can get a
    /// new anchor key) without treating it as a room change: no dwell/settle check, no announcement.</summary>
    public void Rekey(RoomKey from, RoomKey to)
    {
        if (_current == from) _current = to;
    }

    private void DropCandidate()
    {
        _candidate = RoomKey.Invalid;
        _candidateSinceMs = -1;
    }
}
