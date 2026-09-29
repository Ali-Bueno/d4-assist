namespace D4Assist.Navigation;

/// <summary>
/// Entry point wiring the map, room model, hysteresis and announcement policy
/// together. Feed it tile/player snapshots; it raises <see cref="Announce"/> when
/// an automatic announcement is due.
/// </summary>
public sealed class NavigationEngine
{
    /// <summary>How far past a room's footprint the player can stray and still count as "in" it for
    /// announcement purposes: ~4 world units is ~0.4s of walking at the measured ~10 units/s (live log
    /// 2026-09-28), enough to absorb standing/fighting near a tile boundary without flapping between
    /// rooms every poll.</summary>
    public const float RoomStickinessUnits = 4f;

    private readonly DungeonMap _map = new();
    private readonly RoomHysteresis _hysteresis = new();
    private readonly AnnouncementPolicy _policy;
    private readonly ITileWalkability? _walkability;

    private Room? _current;
    private Room? _previous;
    private RoomKey? _entranceKey;

    /// <summary>Toggles and phrasing options for automatic announcements.</summary>
    public AnnouncementPolicy Settings => _policy;

    /// <summary>
    /// Whether an automatic announcement interrupts current speech. Default false:
    /// automatic announcements must not cut off the game's own NVDA speech.
    /// </summary>
    public bool Interrupt { get; set; } = false;

    /// <summary>Raised with the text to speak and whether it should interrupt.</summary>
    public event Action<string, bool>? Announce;

    /// <summary>Per-opening routes behind the most recent announcement (side/position/width/path/fallback
    /// — --debug diagnostics read this right after <see cref="Announce"/> fires).</summary>
    public IReadOnlyList<OpeningRoute> LastOpenings { get; private set; } = [];

    /// <param name="walkability">Optional host-supplied tile walkability; null keeps the pre-fix
    /// side-midpoint/straight-line behaviour everywhere (e.g. tests with no grid data).</param>
    /// <param name="language">Language of the spoken announcements; null means Spanish.</param>
    /// <param name="isExitCandidate">Scene SNOs that can host the floor exit, for the guide re-check mode; null for none.</param>
    public NavigationEngine(ITileWalkability? walkability = null, LanguageSetting? language = null, Func<int, bool>? isExitCandidate = null)
    {
        _policy = new AnnouncementPolicy(language);
        _walkability = walkability;
        Guide = new ExplorationGuide(_map, walkability, isExitCandidate);
    }

    /// <summary>Leads to the nearest unexplored part of the dungeon; "explored" means the player stood in the tile or saw it from a connected doorway.</summary>
    public ExplorationGuide Guide { get; }

    /// <summary>Feed the current loaded-tile window.</summary>
    public void OnTiles(IReadOnlyList<TileInput> tiles) => _map.AddOrUpdate(tiles);

    /// <summary>Feed the live player position; may raise <see cref="Announce"/>.</summary>
    public void OnPlayer(PlayerInput player, TimeSpan now)
    {
        var nowMs = (long)now.TotalMilliseconds;
        _map.MarkVisited(player.X, player.Y);
        if (!RoomModel.TryGetRoom(_map, player.X, player.Y, out var room, _walkability))
        {
            _hysteresis.Observe(RoomKey.Invalid, nowMs); // off-grid: not knowing is not leaving
            return;
        }

        if (_current is not null && room.Key != _current.Key && SharesATile(room, _current))
        {
            // The room grew a new member tile and its anchor (lowest-origin tile) key changed —
            // it's still the same room, not a transition. Relabel it in place, silently.
            var oldKey = _current.Key;
            _hysteresis.Rekey(oldKey, room.Key);
            if (_entranceKey == oldKey) _entranceKey = room.Key;
            _current = room;
            return;
        }

        if (_current is not null && room.Key != _current.Key && _current.IsNear(player.X, player.Y, RoomStickinessUnits))
        {
            // Still within the old room's stickiness margin: treat this poll as "still current room"
            // so a brief step across the boundary doesn't start a room-change candidate (anti-flapping).
            _hysteresis.Observe(_current.Key, nowMs);
            return;
        }

        if (!_hysteresis.Observe(room.Key, nowMs)) return;

        _previous = _current;
        _current = room;
        _entranceKey ??= room.Key;

        var (text, openings) = _policy.Describe(room, player, _previous, isEntrance: room.Key == _entranceKey);
        if (text is null) return;
        LastOpenings = openings;
        Announce?.Invoke(text, Interrupt);
    }

    // Rooms are connected components of same-NodeId tiles; two Room instances that share a member
    // tile (by grid cell, not reference — DungeonMap replaces tile instances on every update) are the
    // same room observed before/after it grew.
    private static bool SharesATile(Room a, Room b) =>
        a.Tiles.Any(t => b.Tiles.Any(o => o.CellX == t.CellX && o.CellY == t.CellY));

    /// <summary>Discard all accumulated state. Call on dungeon change.</summary>
    public void Reset()
    {
        _map.Reset();
        Guide.Reset();
        _hysteresis.Reset();
        _policy.Reset();
        _current = null;
        _previous = null;
        _entranceKey = null;
    }
}
