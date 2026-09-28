namespace D4Assist.Radar;

/// <summary>
/// Erases small free-standing blocked islands the player can step around, port of D2R StructuralWorld's clutter
/// filter (d2r-access src/radar_structure.cpp:19-130). D4's walk map carries no collision kinds, so every
/// blocked island is a candidate; the size, island and detour tests are D2R's.
/// </summary>
internal static class ClutterFilter
{
    private readonly record struct Bounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left + 1;
        public int Height => Bottom - Top + 1;
        public bool Holds(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
        public Bounds Include(int x, int y) => new(Math.Min(Left, x), Math.Min(Top, y), Math.Max(Right, x), Math.Max(Bottom, y));
    }

    public static void Apply(LocalGrid grid, int maxSpan, int bodyRadius)
    {
        var side = grid.Side;
        var original = grid.CopyCells();
        var visited = new bool[original.Length];
        var component = new List<int>();
        var margin = bodyRadius + 1; // one body reach around the island (radar_structure.cpp:23,36)
        for (var start = 0; start < original.Length; start++)
        {
            if (visited[start] || original[start] != RadarCell.Blocked) continue;
            component.Clear();
            component.Add(start);
            visited[start] = true;
            var bounds = new Bounds(start % side, start / side, start % side, start / side);
            var complete = true;
            for (var cursor = 0; cursor < component.Count; cursor++)
            {
                int x = component[cursor] % side, y = component[cursor] / side;
                bounds = bounds.Include(x, y);
                // Eight-connected components preserve diagonal walls as a unit (radar_structure.cpp:113).
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= side || ny >= side) { complete = false; continue; }
                        var next = ny * side + nx;
                        if (original[next] == RadarCell.Unknown) { complete = false; continue; }
                        if (visited[next] || original[next] != RadarCell.Blocked) continue;
                        visited[next] = true;
                        component.Add(next);
                    }
            }
            if (!complete || bounds.Width > maxSpan || bounds.Height > maxSpan) continue;
            if (!StandsAlone(original, side, bounds, margin) || !CanBypass(original, side, bounds, margin)) continue;
            foreach (var index in component) grid.Clear(index);
        }
    }

    // No other non-open cell within one body reach of the island (radar_structure.cpp:19-30).
    private static bool StandsAlone(RadarCell[] cells, int side, Bounds island, int margin)
    {
        for (var y = island.Top - margin; y <= island.Bottom + margin; y++)
            for (var x = island.Left - margin; x <= island.Right + margin; x++)
                if (!island.Holds(x, y) && At(cells, side, x, y) != RadarCell.Open) return false;
        return true;
    }

    // Every open cell around the island connects: a real detour, no enclosed pocket (radar_structure.cpp:32-68).
    private static bool CanBypass(RadarCell[] cells, int side, Bounds island, int margin)
    {
        int left = island.Left - margin, top = island.Top - margin;
        int width = island.Width + 2 * margin, height = island.Height + 2 * margin;
        var region = new RadarCell[width * height];
        var entrance = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                region[index] = At(cells, side, left + x, top + y);
                if (region[index] == RadarCell.Unknown) return false;
                if ((x == 0 || y == 0 || x == width - 1 || y == height - 1) && region[index] == RadarCell.Open) entrance = index;
            }
        if (entrance < 0) return false;

        var reached = new List<int> { entrance };
        region[entrance] = RadarCell.Blocked;
        for (var cursor = 0; cursor < reached.Count; cursor++)
        {
            int x = reached[cursor] % width, y = reached[cursor] / width;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    var next = ny * width + nx;
                    if (region[next] != RadarCell.Open) continue;
                    region[next] = RadarCell.Blocked;
                    reached.Add(next);
                }
        }
        return Array.IndexOf(region, RadarCell.Open) < 0;
    }

    private static RadarCell At(RadarCell[] cells, int side, int x, int y) =>
        x < 0 || y < 0 || x >= side || y >= side ? RadarCell.Unknown : cells[y * side + x];
}
