using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Keeps which bounty pins the open world map holds (MapBountyTableReader): the table is there only while the map is open and the first look takes
/// about 10 s, so it is read in the background as the map opens and again every minute, never on a key press. The whispers' list asks only for what the last
/// read found.
/// </summary>
/// <param name="read">One read of the table; null while no session is open.</param>
public sealed class MapBountyWatch(Func<IReadOnlySet<int>?> read, Action<string>? log = null, Action<Action>? run = null, Func<DateTime>? now = null)
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private bool _busy, _open;
    private DateTime _last;
    private IReadOnlySet<int> _pins = new HashSet<int>();

    /// <summary>The pins of the last read since the map opened; empty before it is done, and with the map closed.</summary>
    public IReadOnlySet<int> Pins
    {
        get { lock (_gate) return _pins; }
    }

    /// <summary>Takes the open menus (GameSession.MenusRead).</summary>
    public void OnMenus(IReadOnlyList<string> open)
    {
        lock (_gate)
        {
            bool mapOpen = open.Contains(MenuScreens.Map);
            var time = now?.Invoke() ?? DateTime.UtcNow;
            if (mapOpen == _open && (!mapOpen || time - _last < Interval)) return;
            bool opened = mapOpen && !_open;
            _open = mapOpen;
            if (!mapOpen || opened) _pins = new HashSet<int>();
            if (!mapOpen || _busy) return;
            (_busy, _last) = (true, time);
        }
        (run ?? (work => Task.Run(work)))(ReadOnce);
    }

    private void ReadOnce()
    {
        IReadOnlySet<int>? pins = null;
        try { pins = read(); }
        catch (Exception e) { log?.Invoke($"MAP BOUNTIES unreadable: {e.Message}"); }
        lock (_gate)
        {
            _busy = false;
            if (!_open || pins is null) return;
            if (!pins.SetEquals(_pins)) log?.Invoke($"MAP BOUNTIES {pins.Count} held: {string.Join(", ", pins)}");
            _pins = pins;
        }
    }
}
