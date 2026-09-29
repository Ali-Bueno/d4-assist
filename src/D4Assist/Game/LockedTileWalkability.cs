using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>Serializes an ITileWalkability shared by several bridges: they run on different poll threads, and the
/// CASC-backed lookup and its cache are not thread-safe.</summary>
/// <param name="cascGate">The one lock every CASC reader in the app shares (the SnoStore is not thread-safe either).</param>
public sealed class LockedTileWalkability(ITileWalkability inner, object cascGate) : ITileWalkability
{
    public bool TryGet(int sceneSno, out WalkGrid grid)
    {
        lock (cascGate) return inner.TryGet(sceneSno, out grid);
    }
}
