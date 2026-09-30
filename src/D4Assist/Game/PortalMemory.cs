using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the exit beacons (portals, warp pads) seen in the current dungeon instance and which ones the player has
/// come or gone through: those lead back, the others lead on. Kept across warps inside the same instance.
/// </summary>
public sealed class PortalMemory
{
    /// <summary>A portal this close to where the player appeared, or vanished, is the one they used. Unmeasured: D4
    /// places the player just beside the portal they came through; `PORTAL seen` logs the real distance.</summary>
    public const float UsedRadiusUnits = 15f;

    /// <summary>A jump this long between two position polls is a warp, not walking: the radar's teleport distance
    /// (RadarSettings.TeleportDistance, d2r-access src/radar.h:80).</summary>
    public const float WarpDistanceUnits = 20f;

    // A placed gizmo never moves; a re-sighting (new actor id after a warp) lands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Portal(int sno, string name, (float X, float Y) position)
    {
        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public (float X, float Y) Position { get; } = position;
        public string Id { get; } = $"portal:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";
        public bool Used { get; set; }
    }

    private readonly Func<ActorState, bool> _isBeacon;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<Portal> _portals = [];
    private readonly List<(float X, float Y)> _arrivals = [];
    private (float X, float Y)? _last;
    private bool _freshInSight;

    /// <param name="isBeacon">True for an actor that gets the exit beacon (LoopTargetSelector).</param>
    /// <param name="log">Debug sink for sightings and uses; null when not debugging.</param>
    public PortalMemory(IDungeonFeed feed, Func<ActorState, bool> isBeacon, Func<int, string?> nameOf, Action<string>? log = null)
    {
        _isBeacon = isBeacon;
        _nameOf = nameOf;
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) Reset(); };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
    }

    /// <summary>True while a portal the player has not used yet is in sight (its beacon then leads, not the guide).</summary>
    public bool FreshInSight { get { lock (_gate) return _freshInSight; } }

    /// <summary>Every portal seen here: unused ones first (tier 0), then the ones the player came or went through (tier 1).</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate) return [.. _portals.Select(p => new GuideDestination(p.Id, p.Position, p.Used ? 1 : 0))];
    }

    /// <summary>Every portal seen here, to outlive a restart of the app.</summary>
    public IReadOnlyList<SavedPortal> Export()
    {
        lock (_gate) return [.. _portals.Select(p => new SavedPortal(p.Sno, p.Name, p.Position.X, p.Position.Y, p.Used))];
    }

    /// <summary>Adds back the portals an earlier run saw in this instance; a used mark is never lost.</summary>
    public void Restore(IEnumerable<SavedPortal> saved)
    {
        lock (_gate)
            foreach (var s in saved)
            {
                var known = _portals.FirstOrDefault(p => p.Sno == s.Sno && Distance(p.Position, (s.X, s.Y)) <= SameSpotUnits);
                if (known is null) _portals.Add(new Portal(s.Sno, s.Name, (s.X, s.Y)) { Used = s.Used });
                else known.Used |= s.Used;
            }
    }

    private void Reset()
    {
        _portals.Clear();
        _arrivals.Clear();
        _last = null;
        _freshInSight = false;
    }

    // The first position in the world, and each one after a warp, is an arrival; the spot left by a warp was a portal taken.
    private void OnPlayer((float X, float Y) here)
    {
        var previous = _last;
        _last = here;
        if (previous is { } left)
        {
            if (Distance(left, here) < WarpDistanceUnits) return;
            foreach (var portal in _portals.Where(p => !p.Used && Distance(p.Position, left) <= UsedRadiusUnits)) MarkUsed(portal, "went through");
        }
        _arrivals.Add(here);
        foreach (var portal in _portals.Where(p => !p.Used && Distance(p.Position, here) <= UsedRadiusUnits)) MarkUsed(portal, "arrived by");
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        bool fresh = false;
        foreach (var actor in actors.Where(_isBeacon))
        {
            var portal = _portals.FirstOrDefault(p => p.Sno == actor.ActorSno && Distance(p.Position, (actor.X, actor.Y)) <= SameSpotUnits)
                ?? Add(actor);
            fresh |= !portal.Used;
        }
        _freshInSight = fresh;
    }

    private Portal Add(ActorState actor)
    {
        var portal = new Portal(actor.ActorSno, _nameOf(actor.ActorSno) ?? actor.ActorSno.ToString(), (actor.X, actor.Y));
        portal.Used = _arrivals.Any(a => Distance(a, portal.Position) <= UsedRadiusUnits);
        _portals.Add(portal);
        string distance = _last is { } p ? $"{Distance(p, portal.Position):F1}" : "?";
        _log?.Invoke($"PORTAL seen: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1}) distance={distance} {(portal.Used ? "arrived by" : "new")}");
        return portal;
    }

    private void MarkUsed(Portal portal, string how)
    {
        portal.Used = true;
        _log?.Invoke($"PORTAL {how}: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1})");
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A remembered portal as ExplorationStore saves it.</summary>
public sealed record SavedPortal(int Sno, string Name, float X, float Y, bool Used);
