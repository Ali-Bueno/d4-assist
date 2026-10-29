using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Saves the current dungeon's map, explored tiles, portals, objectives heard and scene layers' tiles (ExplorationStore) and restores them when the app restarts
/// in the same instance of the same game run, so the guide never leads back over explored ground and the arrival portal
/// is not taken for a new one. A trip to town keeps it for the way back; the next dungeon's save replaces it.
/// </summary>
public sealed class ExplorationPersistence
{
    private readonly ExplorationStore _store;
    private readonly NavigationBridge _navigation;
    private readonly PortalMemory? _portals;
    private readonly ObjectiveMemory? _objectives;
    private readonly SceneLayerPlacement? _layers;
    private readonly Func<DateTime?> _gameStartedUtc;
    private readonly Action<string>? _log;
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private (string World, string Instance, DateTime GameStartedUtc)? _key;
    private ExplorationSnapshot? _pending;
    // The save names another instance of the same world in the same game run: a death can move the player to one with the same
    // tiles (live 2026-10-22, "Die Leere": 3145731, then 3670019 beside the boss; the guide explored the whole dungeon again).
    private bool _otherInstance;
    /// <summary>The tiles a save brought back, beyond the loaded window: what learns of tiles only as the game streams them
    /// (BossRoomDestination) learns of these too (live 2026-10-22: restored far from the boss room, the guide led to the exit).</summary>
    public event Action<IReadOnlyList<TileState>>? Restored;

    /// <summary>Loaded tiles that must agree with another instance's save before it is taken for this one.</summary>
    public const int SameLayoutTiles = 3;

    /// <param name="gameStartedUtc">When the running game process started; null when unknown (then nothing is kept).</param>
    public ExplorationPersistence(
        IDungeonFeed feed, ExplorationStore store, NavigationBridge navigation, PortalMemory? portals, Func<DateTime?> gameStartedUtc,
        Action<string>? log = null, ObjectiveMemory? objectives = null, SceneLayerPlacement? layers = null)
    {
        _store = store;
        _navigation = navigation;
        _portals = portals;
        _objectives = objectives;
        _layers = layers;
        _gameStartedUtc = gameStartedUtc;
        _log = log;

        Enter(feed); // app started mid-dungeon
        // A town trip keeps the save: back through the town portal, the same dungeon restores it (live 2026-10-22, "Die Leere": the
        // trip to the blacksmith deleted it, and the guide explored the whole dungeon again).
        feed.WorldChanged += (_, _, _) => { lock (_gate) Enter(feed); };
        feed.TilesUpdated += tiles => { lock (_gate) OnTiles(tiles); };
    }

    // Only this world can have a save to restore: the one of this game run, of this instance or, its tiles agreeing, another.
    private void Enter(IDungeonFeed feed)
    {
        (_pending, _otherInstance) = (null, false);
        _key = KeyOf(feed);
        if (_key is { } key && _store.Load() is { } saved && (saved.World, saved.GameStartedUtc) == (key.World, key.GameStartedUtc))
            (_pending, _otherInstance) = (saved, saved.Instance != key.Instance);
    }

    private (string, string, DateTime)? KeyOf(IDungeonFeed feed) =>
        feed.CurrentWorldKind == WorldKind.Dungeon && feed.CurrentWorld is { } world && feed.CurrentInstance is { } instance &&
        _gameStartedUtc() is { } started
            ? (world.Name, instance, started)
            : null;

    // The navigation bridge has taken these tiles already (it subscribed first), so a restore merges into the live window.
    private void OnTiles(IReadOnlyList<TileState> tiles)
    {
        if (_key is not { } key) return;
        if (_pending is { } saved)
        {
            _pending = null;
            Restore(saved, tiles);
        }
        var state = _navigation.ExportExploration();
        _store.Save(new ExplorationSnapshot(
            key.World, key.Instance, key.GameStartedUtc, state.Tiles, [.. state.Visited.Select(c => new[] { c.X, c.Y })], _portals?.Export() ?? [],
            _objectives?.Export() ?? [], _layers?.Export() ?? []));
    }

    private void Restore(ExplorationSnapshot saved, IReadOnlyList<TileState> live)
    {
        // CWorldIDs repeat across game sessions: a save whose tiles disagree with the loaded ones is another layout.
        bool sameLayout = live.All(t => saved.Tiles.FirstOrDefault(s => s.OriginX == t.OriginX && s.OriginY == t.OriginY) is not { } s || s.SceneSno == t.SceneSno)
            && (!_otherInstance || live.Count(t => saved.Tiles.Any(s => s.OriginX == t.OriginX && s.OriginY == t.OriginY)) >= SameLayoutTiles);
        if (!sameLayout)
        {
            _log?.Invoke("Exploration: the saved map doesn't match this dungeon; starting fresh.");
            return;
        }
        _navigation.RestoreExploration(new ExplorationState(saved.Tiles, [.. saved.Visited.Where(c => c.Length == 2).Select(c => (c[0], c[1]))]));
        Restored?.Invoke([.. saved.Tiles.Select(t => new TileState(
            t.SceneSno, t.Name, t.OriginX, t.OriginY, t.SizeX, t.SizeY, t.ExitMask, t.NodeId, t.GroupId, t.YawDegrees, t.AuthoredExitMask))]);
        _portals?.Restore(saved.Portals);
        _objectives?.Restore(saved.Objectives ?? []);
        _layers?.Restore(saved.Layers ?? []);
        _log?.Invoke($"Exploration: restored {(_otherInstance ? $"instance {saved.Instance}'s " : "")}{saved.Tiles.Count} tiles ({saved.Visited.Count} explored), {saved.Portals.Count} portals and " +
            $"{saved.Objectives?.Count ?? 0} objectives from the last run.");
    }
}
