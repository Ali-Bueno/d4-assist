using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>A cue to play: one ray's newly confirmed reading.</summary>
public readonly record struct RadarCue(RadarRay Ray, RadarCueKind Kind, bool Unexplored);

/// <summary>
/// The reactive radar: four fixed screen rays, each scanned every poll and fed to its sensor. Port of D2R
/// Radar::Engine::Tick (d2r-access src/radar.cpp:172-214) over an <see cref="IRadarWorld"/>. D4 dungeons are
/// always "maze" levels: one room is one chamber. Single-threaded; the caller serializes calls.
/// </summary>
public sealed class RadarEngine
{
    private RadarSensor[] _sensors;
    private RadarVector _previous;
    private long _lastTickMs;
    private long _lastMoveMs;
    private bool _positioned;
    private int _room;
    private bool _roomKnown;

    public RadarEngine(RadarSettings settings)
    {
        Settings = settings;
        _sensors = NewSensors();
    }

    public RadarSettings Settings { get; }

    /// <summary>The current doorway width (RadarSettings.WayThrough of the world's typical opening).</summary>
    public double WayThrough { get; private set; }

    public RadarSensor Sensor(RadarRay ray) => _sensors[(int)ray];

    /// <summary>Discards every observation; the next known observation is a silent baseline.</summary>
    public void Reset()
    {
        _sensors = NewSensors();
        _positioned = false;
        _roomKnown = false;
        _lastMoveMs = 0;
    }

    /// <summary>One poll. Returns the cues confirmed now, in ray order.</summary>
    public IReadOnlyList<RadarCue> Tick(IRadarWorld world, double playerX, double playerY, long nowMs)
    {
        var motion = new RadarVector(playerX, playerY);
        var rebase = !_positioned || nowMs < _lastTickMs || nowMs - _lastTickMs > Settings.MaxGapMs
            || motion.DistanceTo(_previous) >= Settings.TeleportDistance;
        if (rebase)
        {
            Reset();
            _positioned = true;
        }
        var step = rebase ? default : motion - _previous;
        if (step != default) _lastMoveMs = nowMs; // Engine::UpdateMotion's lastMove_ (radar.cpp:29)
        _previous = motion;
        _lastTickMs = nowMs;

        WayThrough = Settings.WayThrough(world.TypicalOpeningWidth);
        int cellX = (int)Math.Floor(playerX), cellY = (int)Math.Floor(playerY);
        TrackRoom(world, cellX, cellY, motion, step);

        var grid = new LocalGrid(world, cellX, cellY, Settings.StructuralRadius);
        if (Settings.RemoveClutter) ClutterFilter.Apply(grid, Settings.ClutterSpan, Settings.BodyRadius);
        var doorways = DoorwaysAround(world, world.RoomAt(cellX, cellY), FloorAround(world, cellX, cellY), motion);

        var cues = new List<RadarCue>();
        foreach (var ray in RadarRays.All)
        {
            var sensor = _sensors[(int)ray];
            var axis = RadarRays.World(ray, Settings.Frame);
            var query = new PassageQuery(Settings.OpenDepth, Settings.CloseDepth, sensor.State, WayThrough,
                Settings.LateralTolerance, Settings.PassageRadius, doorways);
            var passage = PassageScanner.Scan(grid, axis, query);
            passage = passage with { Unexplored = Qualify(passage, world) };
            if (sensor.State is { } committed && committed.IsObstacle() && passage.Observed == RadarCueKind.Open
                && Wavers(grid, axis, query))
                passage = passage with { Observed = committed, Unexplored = sensor.Unexplored };
            if (sensor.Observe(passage, axis, motion, nowMs, _lastMoveMs) is { } kind)
                cues.Add(new RadarCue(ray, kind, sensor.LastCueUnexplored));
        }
        return cues;
    }

    /// <summary>
    /// The unexplored qualifier (Engine::Qualify, radar.cpp:161-171): an exit into a room never stood in, or,
    /// in D4, a route that runs into unloaded cells. D4 has no open-ground frontier, so floor never qualifies.
    /// </summary>
    public static bool Qualify(Passage passage, IRadarWorld world)
    {
        if (!passage.Available || passage.Observed != RadarCueKind.Exit) return false;
        if (passage.ViaDoorway && passage.CrossingRoom < 0) return true; // the doorway leads beyond the loaded tiles
        return passage.LeadsUnknown || (passage.CrossingRoom >= 0 && !world.IsVisited(passage.CrossingRoom));
    }

    // D4 deviation (user, 2026-09-30): a committed wall gives way to floor only when the floor holds one lane to either side
    // too. The band is anchored to the player's cell, so a wall grazing its edge otherwise flips with every cell walked.
    private static bool Wavers(LocalGrid grid, RadarVector axis, PassageQuery query)
    {
        var lane = PassageScanner.LaneSpacing(axis);
        return new[] { -lane, lane }.Any(offset => PassageScanner.Scan(grid, axis, query with { Offset = offset }).Observed?.IsObstacle() == true);
    }

    // The player's room's doorways, relative to the player like the scan's cells. D4: only those on the player's floor while one is,
    // as for the guide; a wall-less swamp tile's floor islands at its edges read as exits with no way there (research/dungeon-data.md §4.1d).
    private static List<RadarDoorway> DoorwaysAround(IRadarWorld world, int room, int floor, RadarVector player)
    {
        if (room < 0) return [];
        var doorways = world.Doorways.Where(d => d.Room == room).ToList();
        if (floor >= 0 && doorways.Any(d => d.Floor == floor)) doorways = doorways.FindAll(d => d.Floor < 0 || d.Floor == floor);
        return doorways.ConvertAll(d => d with { Centre = d.Centre - player });
    }

    // The floor under the player, or the nearest around them when the 1-unit map puts their feet on a wall cell.
    private int FloorAround(IRadarWorld world, int x, int y)
    {
        for (var reach = 0; reach <= Settings.BodyRadius; reach++)
            for (var dx = -reach; dx <= reach; dx++)
                for (var dy = -reach; dy <= reach; dy++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == reach && world.FloorAt(x + dx, y + dy) is >= 0 and var floor)
                        return floor;
        return -1;
    }

    // Engine::TrackRoom (radar.cpp:49-70): on a room change the ray most against the crossing step is the way back.
    private void TrackRoom(IRadarWorld world, int cellX, int cellY, RadarVector motion, RadarVector travel)
    {
        foreach (var sensor in _sensors)
            if (sensor.WayBack && motion.DistanceTo(sensor.WayBackAt) > WayThrough) sensor.WayBack = false;
        var room = world.RoomAt(cellX, cellY);
        if (room < 0) return; // off-grid is not a room change
        var changed = _roomKnown && room != _room;
        _room = room;
        _roomKnown = true;
        if (!changed) return;

        foreach (var sensor in _sensors) sensor.WayBack = false;
        RadarSensor? back = null;
        double best = 0;
        foreach (var ray in RadarRays.All)
        {
            var along = travel.Dot(RadarRays.World(ray, Settings.Frame));
            if (along < best) { best = along; back = _sensors[(int)ray]; }
        }
        if (back is null) return;
        back.WayBack = true;
        back.WayBackAt = motion;
    }

    private RadarSensor[] NewSensors() => [.. RadarRays.All.Select(_ => new RadarSensor(Settings))];
}
