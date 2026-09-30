namespace D4Assist.Navigation;

/// <summary>
/// Every tile ever seen in the current dungeon. Tiles arrive ~1 Hz as the current
/// loaded window (about two tiles around the player); a tile that drops out of
/// that window stays here until <see cref="Reset"/> (a dungeon change).
/// </summary>
public sealed class DungeonMap
{
    private readonly Dictionary<(int X, int Y), MapTile> _tilesByOrigin = new();
    private readonly Dictionary<(int X, int Y), MapTile> _cellIndex = new();
    private readonly HashSet<(int X, int Y)> _visited = new();

    /// <summary>Bumps whenever a tile is new or changed, or a tile is first visited — lets consumers cache derived data.</summary>
    public int Version { get; private set; }

    /// <summary>Add or replace tiles from the latest loaded-window snapshot.</summary>
    public void AddOrUpdate(IReadOnlyList<TileInput> tiles)
    {
        foreach (var input in tiles)
        {
            var tile = new MapTile(input);
            if (!_tilesByOrigin.TryGetValue((tile.CellX, tile.CellY), out var known) || known.Input != input) Version++;
            _tilesByOrigin[(tile.CellX, tile.CellY)] = tile;
            for (var dx = 0; dx < tile.SpanCellsX; dx++)
                for (var dy = 0; dy < tile.SpanCellsY; dy++)
                    _cellIndex[(tile.CellX + dx, tile.CellY + dy)] = tile;
        }
    }

    /// <summary>Discard every tile. Call on dungeon change.</summary>
    public void Reset()
    {
        _tilesByOrigin.Clear();
        _cellIndex.Clear();
        _visited.Clear();
        Version++;
    }

    /// <summary>Records that the player stood at a world position; returns true when that tile was not visited before.</summary>
    public bool MarkVisited(float worldX, float worldY)
    {
        if (TileAt(worldX, worldY) is not { } tile || !_visited.Add((tile.CellX, tile.CellY))) return false;
        Version++;
        return true;
    }

    /// <summary>Records that the player saw <paramref name="tile"/> from a doorway (ExplorationGuide.SeenRadiusUnits); true when it was not explored before.</summary>
    public bool MarkSeen(MapTile tile)
    {
        if (!_visited.Add((tile.CellX, tile.CellY))) return false;
        Version++;
        return true;
    }

    /// <summary>True when the tile counts as explored: the player stood in it or saw it from a connected doorway (a tile, not merely a loaded one).</summary>
    public bool IsVisited(MapTile tile) => _visited.Contains((tile.CellX, tile.CellY));

    /// <summary>The grid cells of every explored tile.</summary>
    public IReadOnlyCollection<(int X, int Y)> VisitedCells => _visited;

    /// <summary>Adds tiles and explored cells saved by an earlier run; a tile already known from the live window wins.</summary>
    public void Restore(IReadOnlyList<TileInput> tiles, IEnumerable<(int X, int Y)> visited)
    {
        AddOrUpdate([.. tiles.Where(t => !_tilesByOrigin.ContainsKey((GridMath.CellIndex(t.OriginX), GridMath.CellIndex(t.OriginY))))]);
        _visited.UnionWith(visited);
        Version++;
    }

    /// <summary>The tile covering a world position, or null when unseen/unloaded.</summary>
    public MapTile? TileAt(float worldX, float worldY) =>
        _cellIndex.TryGetValue((GridMath.CellIndex(worldX), GridMath.CellIndex(worldY)), out var tile) ? tile : null;

    /// <summary>Every distinct tile accumulated so far.</summary>
    public IReadOnlyCollection<MapTile> Tiles => _tilesByOrigin.Values;
}
