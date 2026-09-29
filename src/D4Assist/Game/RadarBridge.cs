using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Radar;

namespace D4Assist.Game;

/// <summary>
/// Feeds live dungeon snapshots into the radar engine (src/D4Assist.Radar) and plays its confirmed cues. Cues play
/// only in dungeons and radar-only worlds, never in the overworld; they keep running in menus because D4 never pauses.
/// </summary>
internal sealed class RadarBridge
{
    private readonly RadarEngine _engine;
    private readonly DungeonRadarWorld _world;
    private readonly IRadarCuePlayer _player;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads; the engine and world are single-threaded.
    private readonly object _gate = new();
    private readonly bool _diagnostics;
    // D2R plays one poll's cues in its Direction order: up, left, right, down (d2r-access src/radar.h:24-27,
    // radar_synth.cpp:65-70); the player then queues them one after another (CueScheduler).
    private static readonly RadarRay[] CueOrder = [RadarRay.Up, RadarRay.Left, RadarRay.Right, RadarRay.Down];
    private readonly (RadarCueKind? State, bool Unexplored)[] _logged = new (RadarCueKind?, bool)[RadarRays.All.Count];

    public RadarBridge(IDungeonFeed feed, DungeonRadarWorld world, IRadarCuePlayer player, RadarSettings settings, bool diagnostics)
    {
        _engine = new RadarEngine(settings);
        _world = world;
        _player = player;
        _diagnostics = diagnostics;

        feed.WorldChanged += (_, _, _) => { lock (_gate) Reset(); };
        feed.TilesUpdated += tiles => { lock (_gate) { if (_world.UpdateTiles(tiles) && _diagnostics) LogMap(); } };
        // The overworld feeds positions for the beacons but has no tiles: the radar would read it as all unknown.
        feed.PlayerMoved += p => { if (feed.CurrentWorldKind == WorldKind.Overworld) return; lock (_gate) OnPlayer(p); };
    }

    private void Reset()
    {
        _world.Reset();
        _engine.Reset();
        Array.Clear(_logged);
    }

    private void OnPlayer(PlayerState p)
    {
        _world.MarkVisited(p.X, p.Y);
        var cues = _engine.Tick(_world, p.X, p.Y, _clock.ElapsedMilliseconds);
        foreach (var cue in cues.OrderBy(c => Array.IndexOf(CueOrder, c.Ray))) _player.Play(cue.Kind, cue.Ray, cue.Unexplored);
        if (_diagnostics) LogConfirmedChanges(p, cues);
    }

    // One line per confirmed reading change, never per poll.
    private void LogConfirmedChanges(PlayerState p, IReadOnlyList<RadarCue> cues)
    {
        foreach (var ray in RadarRays.All)
        {
            var sensor = _engine.Sensor(ray);
            var current = (sensor.State, sensor.Unexplored);
            if (sensor.State is null || current == _logged[(int)ray]) continue;
            _logged[(int)ray] = current;
            var sounded = cues.Any(c => c.Ray == ray) ? "cue"
                : sensor.Hold == RadarHold.None ? "silent baseline" : $"silent ({sensor.Hold})";
            var passage = sensor.Passage;
            Log($"RADAR {ray}: {sensor.State}{(sensor.Unexplored ? " unexplored" : "")} at ({p.X:F1},{p.Y:F1}) " +
                $"depth={passage.Depth:F1} crossing={passage.Crossing:F1} unknownAt={passage.UnknownDepth:F1} {sounded}");
        }
    }

    private void LogMap() =>
        Log($"RADAR map: {_world.TileCount} tiles, {_world.RoomCount} rooms, {_world.TilesWithoutWalkability} without walkability, " +
            $"doorway={_engine.Settings.WayThrough(_world.TypicalOpeningWidth):F1}");

    private void Log(string message) => Console.WriteLine($"[{_clock.Elapsed.TotalSeconds,7:F1}s] {message}");
}
