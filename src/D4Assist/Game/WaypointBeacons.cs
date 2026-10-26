using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Config;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Loops at the current world's waypoints the player hasn't activated, at their spots in the game data rather than at streamed
/// actors, so one is heard before it is in sight (tester request, 2026-10-23); each falls silent once the game lists it activated.
/// </summary>
/// <param name="waypoints">Every waypoint in the game data (WaypointCatalog.All).</param>
/// <param name="readActivated">The activated waypoints' level areas among those given (ActivatedWaypointsReader); null when unreadable.</param>
/// <param name="currentWorld">The current world SNO.</param>
/// <param name="log">Debug sink for loop start/stop lines; null when not debugging.</param>
/// <param name="runRead">Runs an activation read; null runs it on the thread pool, so the poll thread delivering the player never
/// waits on memory.</param>
public sealed class WaypointBeacons(
    IReadOnlyList<Waypoint> waypoints, Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivated, Func<int> currentWorld,
    Action<string>? log = null, Action<Action>? runRead = null)
{
    /// <summary>Reason logged, and loop key prefix, of the waypoint loops.</summary>
    public const string Reason = "waypoint";

    // While a waypoint not activated is within earshot, its activation is read again this often (one small read, off the poll).
    private static readonly TimeSpan RecheckEvery = TimeSpan.FromSeconds(2);

    private static readonly LoopTarget Target =
        new(LoopKind.Waypoint, LoopTargetSelector.WaypointSound, LoopProfiles.Beacon, Reason, SoundCue.Waypoint);

    private readonly IReadOnlySet<int> _levelAreas = waypoints.Select(w => w.LevelAreaSno).ToHashSet();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _gate = new();
    private readonly HashSet<int> _sounding = [];
    private int? _world;
    private IReadOnlyList<Waypoint> _here = [];
    private IReadOnlySet<int>? _activated;
    private TimeSpan? _readAt;
    private bool _reading, _unreadableLogged;

    /// <summary>The loops for the player at <paramref name="p"/>: this world's waypoints not activated, within the beacon's reach.
    /// Silent until the activated list was read once.</summary>
    public IReadOnlyList<LoopSpot> Spots(PlayerState p)
    {
        lock (_gate)
        {
            int world = currentWorld();
            if (world != _world)
            {
                _world = world;
                _here = [.. waypoints.Where(w => w.WorldSno == world).DistinctBy(w => w.LevelAreaSno)];
                _sounding.Clear();
            }
            var near = _here.Where(w => Distance(w, p) <= Target.Profile.Reach).ToList();
            if (near.Count > 0) RecheckIfDue();
            List<Waypoint> sounding = _activated is { } activated ? [.. near.Where(w => !activated.Contains(w.LevelAreaSno))] : [];
            if (log is not null) LogChanges(sounding, p);
            return [.. sounding.Select(w => new LoopSpot($"{Reason}:{w.LevelAreaSno}", w.Position.X, w.Position.Y, Target))];
        }
    }

    private void RecheckIfDue()
    {
        if (_reading || (_readAt is { } at && _clock.Elapsed - at < RecheckEvery)) return;
        _reading = true;
        _readAt = _clock.Elapsed;
        (runRead ?? (read => Task.Run(read)))(() =>
        {
            IReadOnlyList<int>? read = null;
            try { read = readActivated(_levelAreas); }
            finally
            {
                lock (_gate)
                {
                    _reading = false;
                    if (read is not null) _activated = read.ToHashSet();
                    else if (!_unreadableLogged) { _unreadableLogged = true; log?.Invoke("WAYPOINT activated list unreadable"); }
                }
            }
        });
    }

    private void LogChanges(List<Waypoint> sounding, PlayerState p)
    {
        foreach (var w in sounding.Where(w => _sounding.Add(w.LevelAreaSno)))
            log!($"WAYPOINT loop started: {w.LevelAreaName} at ({w.Position.X:F1},{w.Position.Y:F1}) distance={Distance(w, p):F1}");
        foreach (var w in _here.Where(w => _sounding.Contains(w.LevelAreaSno) && !sounding.Contains(w)))
        {
            _sounding.Remove(w.LevelAreaSno);
            bool activated = _activated?.Contains(w.LevelAreaSno) == true;
            log!($"WAYPOINT loop stopped: {w.LevelAreaName} {(activated ? "activated" : "out of reach")} distance={Distance(w, p):F1}");
        }
    }

    private static double Distance(Waypoint w, PlayerState p) => Math.Sqrt(Sq(w.Position.X - p.X) + Sq(w.Position.Y - p.Y));

    private static double Sq(double v) => v * v;
}
