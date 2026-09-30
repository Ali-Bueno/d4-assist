namespace D4Assist.Navigation;

/// <summary>The dungeon map as accumulated so far: every tile seen, and the grid cells of the explored ones.</summary>
public sealed record ExplorationState(IReadOnlyList<TileInput> Tiles, IReadOnlyList<(int X, int Y)> Visited);
