namespace D4Assist.Navigation;

/// <summary>
/// Host-supplied walkability lookup, keyed by scene SNO. Kept as an interface so this project stays
/// BCL-only: the host (src\D4Assist) implements it from D4Data's offline SceneInfo/Walkability.Map
/// (CASC read, no live process access), caching per SNO.
/// </summary>
public interface ITileWalkability
{
    /// <summary>The tile's local walkability grid (row = local X, col = local Y), or false if unknown.</summary>
    bool TryGet(int sceneSno, out WalkGrid grid);
}
