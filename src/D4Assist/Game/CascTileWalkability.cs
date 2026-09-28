using D4Assist.Navigation;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// ITileWalkability from D4Data's offline SceneInfo.Walkability.Map (CASC read, no live process
/// access). Map rows/columns already match WalkGrid's row=X/column=Y convention (both come from
/// NavGridReader.Map — see D4Data.Model.WalkableMap's doc comment and dungeon-data.md §4.1).
/// Cached per SNO: scene definitions never change while the app runs.
/// </summary>
public sealed class CascTileWalkability(SceneReader sceneReader) : ITileWalkability
{
    private readonly Dictionary<int, WalkGrid?> _cache = new();

    public bool TryGet(int sceneSno, out WalkGrid grid)
    {
        if (!_cache.TryGetValue(sceneSno, out var cached))
        {
            cached = ReadGrid(sceneSno);
            _cache[sceneSno] = cached;
        }
        grid = cached!;
        return cached is not null;
    }

    private WalkGrid? ReadGrid(int sceneSno)
    {
        var map = sceneReader.Read(sceneSno).Walkability?.Map;
        if (map is null) return null;

        var cells = new bool[map.Rows.Length, map.Rows.Length == 0 ? 0 : map.Rows[0].Length];
        for (var r = 0; r < map.Rows.Length; r++)
            for (var c = 0; c < map.Rows[r].Length; c++)
                cells[r, c] = map.Rows[r][c] == '#';
        return new WalkGrid(map.Resolution, map.Origin[0], map.Origin[1], cells.GetLength(0), cells.GetLength(1), cells);
    }
}
