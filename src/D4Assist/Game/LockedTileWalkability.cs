using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>Serializes an ITileWalkability shared by several bridges: they run on different poll threads, and the
/// CASC-backed lookup and its cache are not thread-safe.</summary>
public sealed class LockedTileWalkability(ITileWalkability inner) : ITileWalkability
{
    private readonly object _gate = new();

    public bool TryGet(int sceneSno, out WalkGrid grid)
    {
        lock (_gate) return inner.TryGet(sceneSno, out grid);
    }
}
