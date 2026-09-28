namespace D4Assist.Navigation;

/// <summary>
/// A tile as stored in the <see cref="DungeonMap"/>. Reference identity (not a
/// record): two loads of the same grid cell replace one another rather than compare equal.
/// </summary>
public sealed class MapTile
{
    public MapTile(TileInput input)
    {
        Input = input;
        CellX = GridMath.CellIndex(input.OriginX);
        CellY = GridMath.CellIndex(input.OriginY);
        SpanCellsX = Math.Max(1, (int)MathF.Round(input.SizeX / GridMath.CellSize));
        SpanCellsY = Math.Max(1, (int)MathF.Round(input.SizeY / GridMath.CellSize));
    }

    public TileInput Input { get; }
    public int CellX { get; }
    public int CellY { get; }
    public int SpanCellsX { get; }
    public int SpanCellsY { get; }

    public bool Contains(float worldX, float worldY) =>
        worldX >= Input.OriginX && worldX < Input.OriginX + Input.SizeX &&
        worldY >= Input.OriginY && worldY < Input.OriginY + Input.SizeY;
}
