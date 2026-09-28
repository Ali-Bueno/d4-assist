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

    /// <summary>Add or replace tiles from the latest loaded-window snapshot.</summary>
    public void AddOrUpdate(IReadOnlyList<TileInput> tiles)
    {
        foreach (var input in tiles)
        {
            var tile = new MapTile(input);
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
    }

    /// <summary>The tile covering a world position, or null when unseen/unloaded.</summary>
    public MapTile? TileAt(float worldX, float worldY) =>
        _cellIndex.TryGetValue((GridMath.CellIndex(worldX), GridMath.CellIndex(worldY)), out var tile) ? tile : null;

    /// <summary>Every distinct tile accumulated so far.</summary>
    public IReadOnlyCollection<MapTile> Tiles => _tilesByOrigin.Values;
}
