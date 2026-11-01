using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Keeps which bounty pins the open world map holds and when bounties end (MapBountyTableReader): the table is there only while the map is open and the first
/// look takes about 10 s, so it is read in the background as the map opens and again every minute, never on a key press. The whispers' list asks only for what
/// the last read found. An end stays known with the map closed: it is a time, right until it passes.
/// </summary>
/// <param name="read">One read of the table; null while no session is open.</param>
public sealed class MapBountyWatch(Func<MapBounties?> read, Action<string>? log = null, Action<Action>? run = null, Func<DateTime>? now = null)
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Dictionary<int, DateTime> _ends = [];
    private bool _busy, _open;
    private DateTime _last;
    private IReadOnlySet<int> _pins = new HashSet<int>();

    /// <summary>The pins of the last read since the map opened; empty before it is done, and with the map closed.</summary>
    public IReadOnlySet<int> Pins
    {
        get { lock (_gate) return _pins; }
    }

    /// <summary>The whole minutes a quest's bounty has left, rounded up; null when its end isn't known or has passed.</summary>
    public int? MinutesLeft(int quest)
    {
        lock (_gate)
        {
            if (!_ends.TryGetValue(quest, out var end)) return null;
            var left = end - Now();
            return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalMinutes) : null;
        }
    }

    /// <summary>Takes the open menus (GameSession.MenusRead).</summary>
    public void OnMenus(IReadOnlyList<string> open)
    {
        lock (_gate)
        {
            bool mapOpen = open.Contains(MenuScreens.Map);
            var time = Now();
            if (mapOpen == _open && (!mapOpen || time - _last < Interval)) return;
            bool opened = mapOpen && !_open;
            _open = mapOpen;
            if (!mapOpen || opened) _pins = new HashSet<int>();
            if (!mapOpen || _busy) return;
            (_busy, _last) = (true, time);
        }
        (run ?? (work => Task.Run(work)))(ReadOnce);
    }

    private DateTime Now() => now?.Invoke() ?? DateTime.UtcNow;

    private void ReadOnce()
    {
        MapBounties? held = null;
        try { held = read(); }
        catch (Exception e) { log?.Invoke($"MAP BOUNTIES unreadable: {e.Message}"); }
        lock (_gate)
        {
            _busy = false;
            if (held is null) return;
            foreach (var (quest, end) in held.Ends) _ends[quest] = end;
            if (!_open) return;
            if (!held.Pins.SetEquals(_pins)) log?.Invoke($"MAP BOUNTIES {held.Pins.Count} held, {held.Ends.Count} with an end: {string.Join(", ", held.Pins)}");
            _pins = held.Pins;
        }
    }
}
