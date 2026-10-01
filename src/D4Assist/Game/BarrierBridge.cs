namespace D4Assist.Game;

/// <summary>
/// Hands the standing barriers (BarrierObstacles) to the guide's routes and the radar grid after every actor poll in a
/// dungeon or story area, only when they change; a new world clears them.
/// </summary>
internal sealed class BarrierBridge
{
    private static readonly IReadOnlySet<(int X, int Y)> None = new HashSet<(int X, int Y)>();

    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private readonly BarrierObstacles _barriers;
    private readonly Action<IReadOnlySet<(int X, int Y)>> _apply;
    private PlayerState? _player;
    private IReadOnlySet<(int X, int Y)> _applied = None;

    /// <param name="apply">Receives the cells every standing barrier covers (1-unit world cells).</param>
    public BarrierBridge(IDungeonFeed feed, BarrierObstacles barriers, Action<IReadOnlySet<(int X, int Y)>> apply)
    {
        (_barriers, _apply) = (barriers, apply);
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _barriers.Reset(); _player = null; Apply(None); } };
        feed.PlayerMoved += p => { lock (_gate) _player = p; };
        // The open world has no guide routes and no radar.
        feed.ActorsUpdated += actors => { if (feed.CurrentWorldKind == WorldKind.Overworld) return; lock (_gate) OnActors(actors); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        if (_player is not { } player) return;
        var cells = _barriers.Update(actors, player);
        if (!cells.SetEquals(_applied)) Apply(cells);
    }

    private void Apply(IReadOnlySet<(int X, int Y)> cells)
    {
        _applied = cells;
        _apply(cells);
    }
}
