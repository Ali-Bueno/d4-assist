using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Keeps which bounty pins the open world map holds, when bounties end and what events come (MapBountyTableReader): the table is there only while the map is
/// open and the first look takes about 2 s, so it is read in the background as the map opens and again every minute, never on a key press. The whispers' list
/// asks only for what the last read found. An end or an event stays known with the map closed: it is a time, right until it passes.
/// </summary>
/// <param name="read">One read of the table; null while no session is open.</param>
/// <param name="canRead">True where the map shows the world's bounties (the open world); elsewhere the map is a dungeon's or a story area's and holds none.</param>
public sealed class MapBountyWatch(
    Func<MapBounties?> read, Action<string>? log = null, Action<Action>? run = null, Func<DateTime>? now = null, Func<bool>? canRead = null)
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>The pins of a read this recent stand for the map just opened until its own read lands: a world boss listed only by its pin
    /// would otherwise drop out for the read's 2 s.</summary>
    public static readonly TimeSpan PinsKeptFor = TimeSpan.FromMinutes(5);

    /// <summary>An end or an event this long past is forgotten.</summary>
    public static readonly TimeSpan KeptAfter = TimeSpan.FromHours(1);

    /// <summary>An event object and its quest name the same spot to within this (live 2026-11-01: five of five under 5 units).</summary>
    public const float SameSpotUnits = 5f;

    private readonly object _gate = new();
    private readonly Dictionary<int, DateTime> _ends = [];
    private readonly HashSet<MapEvent> _events = [];
    private bool _busy, _open, _readSinceOpen;
    private DateTime _last, _pinsRead;
    private IReadOnlySet<int> _pins = new HashSet<int>();

    /// <summary>The pins of the last read: since the map opened, or one up to <see cref="PinsKeptFor"/> before it.</summary>
    public IReadOnlySet<int> Pins
    {
        get { lock (_gate) return _pins; }
    }

    /// <summary>True while the first read since the map opened runs.</summary>
    public bool Reading
    {
        get { lock (_gate) return _busy && !_readSinceOpen; }
    }

    /// <summary>The whole minutes a quest's bounty has left, rounded up; null when its end isn't known or has passed.</summary>
    public int? MinutesLeft(int quest)
    {
        lock (_gate) return _ends.TryGetValue(quest, out var end) ? MinutesUntil(end) : null;
    }

    /// <summary>Whether an event at this spot is known and has not gone past its third time: from when it shows on the map until shortly after it started.</summary>
    public bool EventIsUp(float x, float y)
    {
        lock (_gate)
        {
            var time = Now();
            return EventsAt(x, y).Any(e => e.Warmup <= time && time <= e.Third);
        }
    }

    /// <summary>The whole minutes until an event at this spot starts, rounded up; null when none is known to come.</summary>
    public int? MinutesToStart(float x, float y)
    {
        lock (_gate)
        {
            var time = Now();
            return EventsAt(x, y).Where(e => e.Start > time).OrderBy(e => e.Start).Select(e => MinutesUntil(e.Start)).FirstOrDefault();
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
            if (opened)
            {
                _readSinceOpen = false;
                if (time - _pinsRead > PinsKeptFor) _pins = new HashSet<int>();
            }
            if (!mapOpen || _busy || canRead?.Invoke() == false) return;
            (_busy, _last) = (true, time);
        }
        (run ?? (work => Task.Run(work)))(ReadOnce);
    }

    private DateTime Now() => now?.Invoke() ?? DateTime.UtcNow;

    private int? MinutesUntil(DateTime when)
    {
        var left = when - Now();
        return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalMinutes) : null;
    }

    private IEnumerable<MapEvent> EventsAt(float x, float y) =>
        _events.Where(e => MathF.Sqrt(MathF.Pow(e.X - x, 2) + MathF.Pow(e.Y - y, 2)) <= SameSpotUnits);

    private void ReadOnce()
    {
        MapBounties? held = null;
        try { held = read(); }
        catch (Exception e) { log?.Invoke($"MAP BOUNTIES unreadable: {e.Message}"); }
        lock (_gate)
        {
            _busy = false;
            if (held is null) return;
            var forget = Now() - KeptAfter;
            foreach (var quest in _ends.Where(e => e.Value < forget).Select(e => e.Key).ToList()) _ends.Remove(quest);
            _events.RemoveWhere(e => e.Third < forget);
            foreach (var (quest, end) in held.Ends) _ends[quest] = end;
            foreach (var found in held.Events)
                if (_events.Add(found)) log?.Invoke($"MAP EVENT at ({found.X:F0},{found.Y:F0}): warmup {found.Warmup:HH:mm:ss}, start {found.Start:HH:mm:ss}, third {found.Third:HH:mm:ss} UTC");
            if (!_open) return;
            if (!held.Pins.SetEquals(_pins)) log?.Invoke($"MAP BOUNTIES {held.Pins.Count} held, {held.Ends.Count} with an end: {string.Join(", ", held.Pins)}");
            (_pins, _pinsRead, _readSinceOpen) = (held.Pins, Now(), true);
        }
    }
}
