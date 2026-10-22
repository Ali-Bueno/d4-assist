using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Saves the current dungeon's map, explored tiles, portals, objectives heard and scene layers' tiles (ExplorationStore) and restores them when the app restarts
/// in the same instance of the same game run, so the guide never leads back over explored ground and the arrival portal
/// is not taken for a new one. Leaving the dungeon discards the save.
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

        _key = KeyOf(feed); // app started mid-dungeon: only this world can have a save to restore
        if (_key is { } key && store.Load() is { } saved && (saved.World, saved.Instance, saved.GameStartedUtc) == key) _pending = saved;
        feed.WorldChanged += (_, _, _) =>
        {
            lock (_gate)
            {
                _pending = null;
                _key = KeyOf(feed);
                _store.Delete();
            }
        };
        feed.TilesUpdated += tiles => { lock (_gate) OnTiles(tiles); };
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
        bool sameLayout = live.All(t => saved.Tiles.FirstOrDefault(s => s.OriginX == t.OriginX && s.OriginY == t.OriginY) is not { } s || s.SceneSno == t.SceneSno);
        if (!sameLayout)
        {
            _log?.Invoke("Exploration: the saved map doesn't match this dungeon; starting fresh.");
            return;
        }
        _navigation.RestoreExploration(new ExplorationState(saved.Tiles, [.. saved.Visited.Where(c => c.Length == 2).Select(c => (c[0], c[1]))]));
        _portals?.Restore(saved.Portals);
        _objectives?.Restore(saved.Objectives ?? []);
        _layers?.Restore(saved.Layers ?? []);
        _log?.Invoke($"Exploration: restored {saved.Tiles.Count} tiles ({saved.Visited.Count} explored), {saved.Portals.Count} portals and " +
            $"{saved.Objectives?.Count ?? 0} objectives from the last run.");
    }
}
