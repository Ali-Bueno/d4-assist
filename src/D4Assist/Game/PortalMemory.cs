using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the portals and warp pads of the dungeon instance, seen or placed by a loaded tile, and which ones the player came or
/// went through: those lead back, the others lead on (research/dungeon-objectives.md §18). Kept across warps inside the instance.
/// </summary>
public sealed class PortalMemory
{
    /// <summary>A portal this close to where the player appeared, or vanished, is the one they used. Unmeasured: D4
    /// places the player just beside the portal they came through; `PORTAL seen` logs the real distance.</summary>
    public const float UsedRadiusUnits = 15f;

    /// <summary>A jump this long between two position polls is a warp, not walking: the radar's teleport distance
    /// (RadarSettings.TeleportDistance, d2r-access src/radar.h:80).</summary>
    public const float WarpDistanceUnits = 20f;

    /// <summary>A placed portal still unseen this close, for <see cref="AbsentPolls"/> polls, isn't there yet (portals stream in ~25 units away).</summary>
    public const float AbsentWithinUnits = 12f;

    /// <summary>Actor polls in a row (500 ms each) that must miss it, so the actors streaming in after a warp aren't missed.</summary>
    public const int AbsentPolls = 4;

    // A placed gizmo never moves; a re-sighting (new actor id after a warp) lands on the same spot, and so does the actor its
    // tile places (0.1 units apart live, 2026-10-04).
    private const float SameSpotUnits = 1f;

    private sealed class Portal(int sno, string name, (float X, float Y) position)
    {
        private readonly string _id = $"portal:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";

        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public (float X, float Y) Position { get; } = position;
        // A portal back after being missed is a new goal for the guide, which never returns to a goal it reached.
        public string Id => Returns == 0 ? _id : $"{_id}#{Returns}";
        public bool Used { get; set; }
        /// <summary>The game has shown it (now or in an earlier run); false while only its tile places it.</summary>
        public bool Seen { get; set; }
        /// <summary>Placed by its tile but missing when the player came close (not spawned yet, or a variant left out).</summary>
        public bool Absent { get; set; }
        public int Misses { get; set; }
        public int Returns { get; set; }
        /// <summary>The player stood at it: as an objective it has led them there (until the guide is switched on again).</summary>
        public bool Reached { get; set; }
    }

    private readonly Func<ActorState, bool> _isBeacon;
    private readonly Func<int, string?> _nameOf;
    private readonly Func<int, IReadOnlyList<TileActor>>? _tilePortals;
    private readonly Func<IReadOnlySet<int>>? _objectiveActors;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<Portal> _portals = [];
    private readonly List<(float X, float Y)> _arrivals = [];
    private readonly HashSet<(int Scene, float X, float Y)> _tilesRead = [];
    private (float X, float Y)? _last;
    private bool _freshInSight;
    private bool _predicts;
    private int _generation;

    /// <param name="isBeacon">True for an actor that gets the exit beacon (LoopTargetSelector).</param>
    /// <param name="log">Debug sink for sightings and uses; null when not debugging.</param>
    /// <param name="tilePortals">The portals a scene places, in its own axes (TileActorReader.In); null knows portals only once
    /// seen. Not used in the endgame worlds, whose guide keeps its tuned order (GuideSwitch).</param>
    /// <param name="objectiveActors">The actors the current world's objective names (LoopTargetSelector.WorldObjectiveActors): such a
    /// portal is an objective until used (the Travincal hub's side dungeons); null for none.</param>
    public PortalMemory(
        IDungeonFeed feed, Func<ActorState, bool> isBeacon, Func<int, string?> nameOf, Action<string>? log = null,
        Func<int, IReadOnlyList<TileActor>>? tilePortals = null, Func<IReadOnlySet<int>>? objectiveActors = null)
    {
        _isBeacon = isBeacon;
        _nameOf = nameOf;
        _tilePortals = tilePortals;
        _objectiveActors = objectiveActors;
        _log = log;
        _predicts = tilePortals is not null && (feed.CurrentWorld is not { } world || !GuideSwitch.IsEndgameWorld(world.Name));
        feed.WorldChanged += (_, name, _) => { lock (_gate) Reset(name); };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        if (tilePortals is not null) feed.TilesUpdated += OnTiles;
    }

    /// <summary>True while a portal the player has not used yet is in sight (its beacon then leads, not the guide).</summary>
    public bool FreshInSight { get { lock (_gate) return _freshInSight; } }

    /// <summary>Every portal known here and not found missing: unused ones first (tier 0), then the ones the player came or went
    /// through (tier 1).</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate) return [.. _portals.Where(p => !p.Absent).Select(p => new GuideDestination(p.Id, p.Position, p.Used ? 1 : 0))];
    }

    /// <summary>The portals the world's objective names that the player hasn't used or stood at yet, as dungeon objectives: the
    /// guide leads to them before exploring (ExplorationGuide objectives).</summary>
    public IReadOnlyList<GuideDestination> ObjectiveDestinations()
    {
        if (_objectiveActors?.Invoke() is not { Count: > 0 } named) return [];
        lock (_gate)
            return [.. _portals.Where(p => !p.Used && !p.Absent && !p.Reached && named.Contains(p.Sno))
                .Select(p => new GuideDestination($"objective:{p.Id}", p.Position, ObjectiveMemory.DungeonTier))];
    }

    /// <summary>Lets the objective portals the player already stood at lead again (the guide was just switched on).</summary>
    public void Rearm()
    {
        lock (_gate)
            foreach (var portal in _portals) portal.Reached = false;
    }

    /// <summary>Every portal seen here, to outlive a restart of the app; the ones only their tile places are read again.</summary>
    public IReadOnlyList<SavedPortal> Export()
    {
        lock (_gate) return [.. _portals.Where(p => p.Seen).Select(p => new SavedPortal(p.Sno, p.Name, p.Position.X, p.Position.Y, p.Used))];
    }

    /// <summary>Adds back the portals an earlier run saw in this instance; a used mark is never lost.</summary>
    public void Restore(IEnumerable<SavedPortal> saved)
    {
        lock (_gate)
            foreach (var s in saved)
            {
                var known = Find(s.Sno, (s.X, s.Y));
                if (known is null) _portals.Add(new Portal(s.Sno, s.Name, (s.X, s.Y)) { Used = s.Used, Seen = true });
                else (known.Used, known.Seen, known.Absent) = (known.Used | s.Used, true, false);
            }
    }

    private void Reset(string worldName)
    {
        _portals.Clear();
        _arrivals.Clear();
        _tilesRead.Clear();
        _last = null;
        _freshInSight = false;
        _predicts = _tilePortals is not null && !GuideSwitch.IsEndgameWorld(worldName);
        _generation++;
    }

    // The first position in the world, and each one after a warp, is an arrival; the spot left by a warp was a portal taken.
    private void OnPlayer((float X, float Y) here)
    {
        NoteReached(here);
        var previous = _last;
        _last = here;
        if (previous is { } left)
        {
            if (Distance(left, here) < WarpDistanceUnits) return;
            foreach (var portal in _portals.Where(p => !p.Used && !p.Absent && Distance(p.Position, left) <= UsedRadiusUnits))
                MarkUsed(portal, "went through");
            foreach (var portal in _portals) portal.Misses = 0; // the actors around the new spot are still streaming in
        }
        _arrivals.Add(here);
        foreach (var portal in _portals.Where(p => !p.Used && !p.Absent && Distance(p.Position, here) <= UsedRadiusUnits))
            MarkUsed(portal, "arrived by");
    }

    private void NoteReached((float X, float Y) here)
    {
        foreach (var portal in _portals.Where(p => !p.Reached && Distance(p.Position, here) <= ExplorationGuide.DestinationArrivalUnits))
            portal.Reached = true;
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        bool fresh = false;
        foreach (var actor in actors.Where(_isBeacon))
        {
            var portal = Find(actor.ActorSno, (actor.X, actor.Y)) ?? Add(actor);
            if (!portal.Seen || portal.Absent) Shown(portal);
            fresh |= !portal.Used;
        }
        _freshInSight = fresh;
        if (_last is { } here) CheckPlacedOnly(here);
    }

    // A portal its tile places that is still missing with the player beside it isn't there: the guide stops leading to it.
    private void CheckPlacedOnly((float X, float Y) here)
    {
        foreach (var portal in _portals.Where(p => !p.Seen && !p.Absent))
        {
            if (Distance(portal.Position, here) > AbsentWithinUnits) { portal.Misses = 0; continue; }
            if (++portal.Misses < AbsentPolls) continue;
            portal.Absent = true;
            _log?.Invoke($"PORTAL not there: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1}), placed by its tile");
        }
    }

    private void Shown(Portal portal)
    {
        string was = portal.Absent ? "missing earlier" : "placed by its tile";
        if (portal.Absent) (portal.Returns, portal.Reached) = (portal.Returns + 1, false);
        (portal.Seen, portal.Absent, portal.Misses) = (true, false, 0);
        _log?.Invoke($"PORTAL seen: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1}), {was}");
    }

    private Portal Add(ActorState actor)
    {
        var portal = new Portal(actor.ActorSno, _nameOf(actor.ActorSno) ?? actor.ActorSno.ToString(), (actor.X, actor.Y)) { Seen = true };
        portal.Used = _arrivals.Any(a => Distance(a, portal.Position) <= UsedRadiusUnits);
        _portals.Add(portal);
        string distance = _last is { } p ? $"{Distance(p, portal.Position):F1}" : "?";
        _log?.Invoke($"PORTAL seen: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1}) distance={distance} {(portal.Used ? "arrived by" : "new")}");
        return portal;
    }

    // Tile polls repeat the loaded tiles: each placement is read once. The CASC read runs outside the gate, which the guide
    // takes on every player poll.
    private void OnTiles(IReadOnlyList<TileState> tiles)
    {
        List<TileState> fresh;
        int generation;
        lock (_gate)
        {
            if (!_predicts) return;
            fresh = [.. tiles.Where(t => _tilesRead.Add((t.SceneSno, MathF.Round(t.OriginX), MathF.Round(t.OriginY))))];
            generation = _generation;
        }
        if (fresh.Count == 0) return;
        var placed = fresh.SelectMany(t => InWorld(t, _tilePortals!(t.SceneSno)).Select(p => (Tile: t.Name, p.Sno, p.Position))).ToList();
        if (placed.Count == 0) return;
        lock (_gate)
        {
            if (generation != _generation) return; // the world changed meanwhile
            foreach (var (tile, sno, position) in placed) Place(sno, position, tile);
        }
    }

    // Only unrotated tiles: TileRotation's turn direction is unverified against a live rotated tile, and a wrong spot would
    // send the guide astray. Every DRLG tile seen so far is unrotated (research/client-memory.md §3).
    private static IEnumerable<(int Sno, (float X, float Y) Position)> InWorld(TileState tile, IReadOnlyList<TileActor> portals)
    {
        if (portals.Count == 0 || TileRotation.QuarterTurns(tile.YawDegrees) != 0) yield break;
        foreach (var p in portals) yield return (p.ActorSno, (tile.OriginX + p.Local.X, tile.OriginY + p.Local.Y));
    }

    private void Place(int sno, (float X, float Y) position, string? tile)
    {
        bool entrance = IsEntranceTile(tile);
        if (Find(sno, position) is { } known)
        {
            if (entrance && !known.Used) MarkUsed(known, "the way in"); // restored from a save as unused
            return;
        }
        var portal = new Portal(sno, _nameOf(sno) ?? sno.ToString(), position);
        portal.Used = entrance || _arrivals.Any(a => Distance(a, position) <= UsedRadiusUnits);
        _portals.Add(portal);
        _log?.Invoke($"PORTAL placed by tile {tile}: {portal.Name} at ({position.X:F1},{position.Y:F1}) " +
            (entrance ? "the way in" : portal.Used ? "arrived by" : "new"));
    }

    // A DRLG dungeon's entrance tile (Cave_Tomb_W_Entrance_01) holds the portal the player came in by, though the app may not have
    // seen the arrival (live 2026-10-22, "Die Leere": started beside the boss after a death, it led to the entrance as an unused
    // exit instead of to the boss room).
    private static bool IsEntranceTile(string? tile) => tile?.Contains("_Entrance", StringComparison.Ordinal) == true;

    private Portal? Find(int sno, (float X, float Y) position) =>
        _portals.FirstOrDefault(p => p.Sno == sno && Distance(p.Position, position) <= SameSpotUnits);

    private void MarkUsed(Portal portal, string how)
    {
        portal.Used = true;
        _log?.Invoke($"PORTAL {how}: {portal.Name} at ({portal.Position.X:F1},{portal.Position.Y:F1})");
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A remembered portal as ExplorationStore saves it.</summary>
public sealed record SavedPortal(int Sno, string Name, float X, float Y, bool Used);
