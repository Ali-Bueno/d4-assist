using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>Why the last confirmed change did not sound (D2R Sensor::hold, diagnostics only).</summary>
public enum RadarHold { None, Retreat, WayBack, Unchanged, Interval }

/// <summary>The wall last seen within closing depth, anchored in the world (d2r-access src/radar.h:150-157).</summary>
/// <param name="Projection">The wall's position along the ray axis.</param>
/// <param name="Origin">Where the player stood when it was last seen there.</param>
public readonly record struct Frontier(double Projection, RadarVector Origin);

/// <summary>
/// One direction's state machine, port of D2R Sensor + Engine::Observe/Deliver (d2r-access src/radar.h:158-177,
/// radar.cpp:71-155): silent baseline, confirm-by-travel or stationary confirm, the wall memory, the way-back
/// mark and the opening-only cue interval.
/// </summary>
public sealed class RadarSensor(RadarSettings settings)
{
    private bool _baseline;
    private RadarCueKind? _candidate;
    private bool _candidateUnexplored;
    private RadarVector _candidateOrigin;
    private long _candidateSince;
    private RadarCueKind? _deferred;
    private bool _deferredUnexplored;
    private RadarCueKind? _lastCue;
    private long? _lastOpeningMs;

    /// <summary>The latest scan.</summary>
    public Passage Passage { get; private set; } = new();

    /// <summary>Confirmed reading; null until the baseline.</summary>
    public RadarCueKind? State { get; private set; }

    /// <summary>Qualifier of <see cref="State"/>; a change of it alone is a change too.</summary>
    public bool Unexplored { get; private set; }

    /// <summary>Qualifier of the last cue delivered.</summary>
    public bool LastCueUnexplored { get; private set; }

    public RadarHold Hold { get; private set; }

    public Frontier? Frontier { get; private set; }

    /// <summary>The doorway just crossed: its next exit reading is taken as read (radar.h:174-175).</summary>
    internal bool WayBack { get; set; }
    internal RadarVector WayBackAt { get; set; }

    /// <summary>Feeds one scan; returns the cue to play now, or null. <paramref name="motion"/> is the player's
    /// continuous position, <paramref name="lastMoveMs"/> when it last changed.</summary>
    public RadarCueKind? Observe(Passage passage, RadarVector axis, RadarVector motion, long nowMs, long lastMoveMs)
    {
        Passage = passage;
        if (!passage.Available)
        {
            // Coverage loss keeps the confirmed state and stops any confirmation (radar.cpp:73-77).
            _candidate = null;
            return null;
        }
        if (!_baseline)
        {
            _baseline = true; // the first observation after a reset never sounds
            State = passage.Observed;
            Unexplored = passage.Unexplored;
        }
        else if (passage.Observed == State && passage.Unexplored == Unexplored)
        {
            _candidate = null;
        }
        else if (_candidate != passage.Observed || _candidateUnexplored != passage.Unexplored)
        {
            _candidate = passage.Observed;
            _candidateUnexplored = passage.Unexplored;
            _candidateOrigin = motion;
            _candidateSince = nowMs;
        }
        else
        {
            var travelled = motion.DistanceTo(_candidateOrigin) >= settings.ConfirmDistance;
            var stationary = nowMs - lastMoveMs >= settings.StoppedMs && nowMs - _candidateSince >= settings.StationaryConfirmMs;
            if (travelled || stationary) Confirm(passage);
        }
        TrackWall(passage, axis, motion);
        return Deliver(axis, motion, nowMs);
    }

    // D4 deviation (user, 2026-09-30): the wall memory follows the wall while it is still within closing depth, so a
    // baseline wall, or one walked along past BoundaryRange, still gates a retreat. Frozen once the player backs away.
    private void TrackWall(Passage passage, RadarVector axis, RadarVector motion)
    {
        if (State is { } state && state.IsObstacle() && passage.Observed == state && passage.Depth <= passage.CloseDepth)
            Frontier = new Frontier(motion.Dot(axis) + passage.Depth, motion);
    }

    private void Confirm(Passage passage)
    {
        State = passage.Observed;
        Unexplored = passage.Unexplored;
        _candidate = null;
        _deferred = State; // replaces any older change still waiting
        _deferredUnexplored = Unexplored;
        Hold = RadarHold.None;
        if (State != RadarCueKind.Exit) WayBack = false;
    }

    private RadarCueKind? Deliver(RadarVector axis, RadarVector motion, long nowMs)
    {
        if (_deferred is not { } deferred) return null;
        var passable = deferred.IsPassable();
        if (deferred == RadarCueKind.Open && Frontier is { } frontier)
        {
            // Backing away from an announced wall lengthens the route without opening anything; half the
            // hysteresis band absorbs raster steps. Dropped, not held (radar.cpp:109-125). D4: never an exit, which a
            // retreat cannot create and whose route may end at that same wall (PassageScanner.Classify).
            var lateral = Math.Abs(axis.Across(motion - frontier.Origin));
            var reach = motion.Dot(axis) + Passage.Depth;
            var margin = (Passage.OpenDepth - Passage.CloseDepth) / 2;
            if (lateral <= settings.BoundaryRange && reach < frontier.Projection + margin)
                return Drop(RadarHold.Retreat);
        }
        if (deferred == RadarCueKind.Exit && WayBack)
        {
            WayBack = false;
            _lastCue = RadarCueKind.Exit;
            LastCueUnexplored = _deferredUnexplored;
            _lastOpeningMs = nowMs;
            return Drop(RadarHold.WayBack);
        }
        if (passable && deferred == _lastCue && _deferredUnexplored == LastCueUnexplored) return Drop(RadarHold.Unchanged);
        // Counted from the last opening, never from a wall (radar.cpp:142-147).
        if (passable && _lastOpeningMs is { } lastOpening && nowMs - lastOpening < settings.CueIntervalMs)
        {
            Hold = RadarHold.Interval;
            return null;
        }
        _lastCue = deferred;
        LastCueUnexplored = _deferredUnexplored;
        if (passable) _lastOpeningMs = nowMs;
        _deferred = null;
        Hold = RadarHold.None;
        return deferred;
    }

    private RadarCueKind? Drop(RadarHold reason)
    {
        _deferred = null;
        Hold = reason;
        return null;
    }
}
