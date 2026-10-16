namespace D4Assist.Game;

/// <summary>
/// After every actor poll in a dungeon or story area, hands the standing barriers (BarrierObstacles) to the radar grid, and them
/// with the doors the player can't open yet (LockedDoors) to the guide's routes (the radar sounds every closed door already); each
/// only when it changes. A new world clears them.
/// </summary>
internal sealed class BarrierBridge
{
    private static readonly IReadOnlySet<(int X, int Y)> None = new HashSet<(int X, int Y)>();

    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private readonly BarrierObstacles? _barriers;
    private readonly LockedDoors? _doors;
    private readonly Action<IReadOnlySet<(int X, int Y)>> _applyWalls;
    private readonly Action<IReadOnlySet<(int X, int Y)>> _applyRoutes;
    private PlayerState? _player;
    private IReadOnlySet<(int X, int Y)> _walls = None;
    private IReadOnlySet<(int X, int Y)> _routes = None;

    /// <param name="barriers">Barrier walls; null when they are switched off.</param>
    /// <param name="doors">Doors the player can't open yet; null for none.</param>
    /// <param name="walls">Receives the cells every standing barrier covers (1-unit world cells), for the radar.</param>
    /// <param name="routes">Receives those cells and every locked door's, for the guide's routes.</param>
    public BarrierBridge(
        IDungeonFeed feed, BarrierObstacles? barriers, LockedDoors? doors,
        Action<IReadOnlySet<(int X, int Y)>> walls, Action<IReadOnlySet<(int X, int Y)>> routes)
    {
        (_barriers, _doors, _applyWalls, _applyRoutes) = (barriers, doors, walls, routes);
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _barriers?.Reset(); _doors?.Reset(); _player = null; Apply(None, None, force: true); } };
        feed.PlayerMoved += p => { lock (_gate) _player = p; };
        // The open world has no guide routes and no radar.
        feed.ActorsUpdated += actors => { if (feed.CurrentWorldKind == WorldKind.Overworld) return; lock (_gate) OnActors(actors); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        if (_player is not { } player) return;
        var walls = _barriers?.Update(actors, player) ?? None;
        var doors = _doors?.Update(actors, player) ?? None;
        Apply(walls, doors.Count == 0 ? walls : walls.Union(doors).ToHashSet());
    }

    private void Apply(IReadOnlySet<(int X, int Y)> walls, IReadOnlySet<(int X, int Y)> routes, bool force = false)
    {
        if (force || !walls.SetEquals(_walls))
        {
            _walls = walls;
            _applyWalls(walls);
        }
        if (force || !routes.SetEquals(_routes))
        {
            _routes = routes;
            _applyRoutes(routes);
        }
    }
}
