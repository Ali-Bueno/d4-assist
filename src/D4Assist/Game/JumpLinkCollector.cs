using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// The jump links of the loaded tiles, for the guide: each tile's traversal points (Traversal_Gizmo_*, the one-way slide aside: a
/// jump's two ends, a ladder's foot and top, a duck-under's two sides; in any marker layer: the Philios mindscape's tiles keep them
/// in "Game" and in "Traversals") paired with their mutual nearest within
/// <see cref="MaxPairUnits"/>. Known offline once a tile loads, before the game streams the points in (live 2026-10-18: one
/// tile's floor in four islands joined by three pairs 4.9 units apart; the guide had aimed straight across the gaps).
/// </summary>
/// <param name="jumpsIn">A scene's jump points in its own axes (TileActorReader over every marker layer).</param>
/// <param name="apply">Takes the whole set whenever it changes (NavigationBridge.SetJumpLinks).</param>
public sealed class JumpLinkCollector
{
    /// <summary>The two ends of one jump are this close at most (4.9 seen); the next pair stood 10 or more away.</summary>
    public const float MaxPairUnits = 8f;

    private readonly object _gate = new();
    private readonly Func<int, IReadOnlyList<TileActor>> _jumpsIn;
    private readonly Action<IReadOnlyList<JumpLink>> _apply;
    private readonly Action<string>? _log;
    private readonly Dictionary<(int Scene, float X, float Y), IReadOnlyList<JumpLink>> _byTile = [];
    private int _generation;

    public JumpLinkCollector(IDungeonFeed feed, Func<int, IReadOnlyList<TileActor>> jumpsIn, Action<IReadOnlyList<JumpLink>> apply, Action<string>? log = null)
    {
        _jumpsIn = jumpsIn;
        _apply = apply;
        _log = log;
        feed.WorldChanged += (_, _, _) =>
        {
            lock (_gate)
            {
                _generation++;
                _byTile.Clear();
            }
            _apply([]);
        };
        feed.TilesUpdated += OnTiles;
    }

    // Each placement is read once; the CASC read runs outside the gate.
    private void OnTiles(IReadOnlyList<TileState> tiles)
    {
        List<TileState> fresh;
        int generation;
        lock (_gate)
        {
            fresh = [.. tiles.Where(t => !_byTile.ContainsKey(Key(t)))];
            generation = _generation;
        }
        if (fresh.Count == 0) return;
        var read = fresh.Select(t => (Tile: t, Links: Pair(InWorld(t, _jumpsIn(t.SceneSno))))).ToList();
        IReadOnlyList<JumpLink> all;
        lock (_gate)
        {
            if (generation != _generation) return; // the world changed meanwhile
            foreach (var (tile, links) in read)
            {
                _byTile[Key(tile)] = links;
                if (links.Count > 0)
                    _log?.Invoke($"JUMPS {tile.Name}: " + string.Join(", ", links.Select(l => $"({l.A.X:F1},{l.A.Y:F1})-({l.B.X:F1},{l.B.Y:F1})")));
            }
            if (read.All(r => r.Links.Count == 0)) return;
            all = [.. _byTile.Values.SelectMany(l => l)];
        }
        _apply(all);
    }

    private static (int, float, float) Key(TileState tile) => (tile.SceneSno, MathF.Round(tile.OriginX), MathF.Round(tile.OriginY));

    // Only unrotated tiles, as PortalMemory: every DRLG tile seen so far is unrotated, and a wrong spot would mislead the guide.
    private static List<(float X, float Y)> InWorld(TileState tile, IReadOnlyList<TileActor> jumps) =>
        jumps.Count == 0 || TileRotation.QuarterTurns(tile.YawDegrees) != 0 ? []
        : [.. jumps.Select(j => (tile.OriginX + j.Local.X, tile.OriginY + j.Local.Y)).Distinct()];

    /// <summary>The jump points paired with their mutual nearest within <see cref="MaxPairUnits"/>; a point with no such partner is left out.</summary>
    public static IReadOnlyList<JumpLink> Pair(IReadOnlyList<(float X, float Y)> points)
    {
        int? Nearest(int i)
        {
            int? best = null;
            for (int j = 0; j < points.Count; j++)
                if (j != i && Distance(points[i], points[j]) <= MaxPairUnits && (best is null || Distance(points[i], points[j]) < Distance(points[i], points[best.Value])))
                    best = j;
            return best;
        }
        var links = new List<JumpLink>();
        for (int i = 0; i < points.Count; i++)
            if (Nearest(i) is { } j && j > i && Nearest(j) == i) links.Add(new JumpLink(points[i], points[j]));
        return links;
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}
