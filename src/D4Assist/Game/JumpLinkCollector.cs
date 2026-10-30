using System.Numerics;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>What a tile's traversal marker is, for the guide's links (<see cref="TraversalKinds.Of"/> by actor name).</summary>
public enum TraversalKind { None, Jump, Slide, ClimbUp, ClimbDown, Exit }

public static class TraversalKinds
{
    /// <summary>By actor name: Traversal_Gizmo_Slide, _FreeClimb_Up/_Down and _RungClimb_Up/_Down, the MarkerLocation_TraversalExit
    /// where one lands (build 3.2.3), and every other Traversal_Gizmo_* (a jump's ends, a ladder's foot and top, a duck-under's two
    /// sides: no exit pair of their own) a point to pair.</summary>
    public static TraversalKind Of(string? name) => name switch
    {
        "Traversal_Gizmo_Jump" => TraversalKind.Jump,
        "Traversal_Gizmo_Slide" => TraversalKind.Slide,
        "Traversal_Gizmo_FreeClimb_Up" or "Traversal_Gizmo_RungClimb_Up" => TraversalKind.ClimbUp,
        "Traversal_Gizmo_FreeClimb_Down" or "Traversal_Gizmo_RungClimb_Down" => TraversalKind.ClimbDown,
        "MarkerLocation_TraversalExit" => TraversalKind.Exit,
        _ when name?.StartsWith("Traversal_Gizmo_", StringComparison.Ordinal) == true => TraversalKind.Jump,
        _ => TraversalKind.None,
    };
}

/// <summary>A tile's traversal marker in world units, with the prefab placement that placed it (<see cref="TileActor.Prefab"/>; 0 for none).</summary>
public readonly record struct TraversalMarker(TraversalKind Kind, Vector3 At, int Prefab = 0);

/// <summary>
/// The jump links of the loaded tiles, for the guide: each tile's traversal points (Traversal_Gizmo_*: a
/// jump's two ends, a ladder's foot and top, a duck-under's two sides; in any marker layer: the Philios mindscape's tiles keep them
/// in "Game" and in "Traversals") joined within the traversal prefab that placed them, else paired with their mutual nearest within
/// <see cref="MaxPairUnits"/>. Known offline once a tile loads, before the game streams the points in (live 2026-10-18: one
/// tile's floor in four islands joined by three pairs 4.9 units apart; the guide had aimed straight across the gaps).
/// </summary>
/// <param name="jumpsIn">A scene's jump points in its own axes (TileActorReader over every marker layer).</param>
/// <param name="apply">Takes the whole set whenever it changes (NavigationBridge.SetJumpLinks).</param>
/// <remarks>A slide leads one way to its exit, and a climb joins its top exit to its bottom one (live 2026-10-22, "Die Leere":
/// the pedestal, the slide's foot, the climb's foot and the boss lie on four floors the walk map never joins, with one jump
/// among them; the guide knew only the jump).</remarks>
public sealed class JumpLinkCollector
{
    /// <summary>A climb's two exits stand within this of it (3.1 seen, top and bottom).</summary>
    public const float ClimbExitUnits = 4f;

    /// <summary>The two ends of one jump are this close at most (4.9 seen); the next pair stood 10 or more away.</summary>
    public const float MaxPairUnits = 8f;

    private readonly object _gate = new();
    private readonly Func<int, IReadOnlyList<TileActor>> _jumpsIn;
    private readonly Func<int, TraversalKind> _kindOf;
    private readonly Action<IReadOnlyList<JumpLink>> _apply;
    private readonly Action<string>? _log;
    private readonly Dictionary<(int Scene, float X, float Y), IReadOnlyList<JumpLink>> _byTile = [];
    private int _generation;

    /// <param name="kindOf">What each actor SNO <paramref name="jumpsIn"/> returns is; null takes every one for a jump point.</param>
    public JumpLinkCollector(
        IDungeonFeed feed, Func<int, IReadOnlyList<TileActor>> jumpsIn, Action<IReadOnlyList<JumpLink>> apply, Action<string>? log = null,
        Func<int, TraversalKind>? kindOf = null)
    {
        _jumpsIn = jumpsIn;
        _kindOf = kindOf ?? (_ => TraversalKind.Jump);
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
        var read = fresh.Select(t => (Tile: t, Links: Links(InWorld(t, _jumpsIn(t.SceneSno))))).ToList();
        IReadOnlyList<JumpLink> all;
        lock (_gate)
        {
            if (generation != _generation) return; // the world changed meanwhile
            foreach (var (tile, links) in read)
            {
                _byTile[Key(tile)] = links;
                if (links.Count > 0)
                    _log?.Invoke($"JUMPS {tile.Name}: " + string.Join(", ", links.Select(l => $"({l.A.X:F1},{l.A.Y:F1}){(l.OneWay ? "->" : "-")}({l.B.X:F1},{l.B.Y:F1})")));
            }
            if (read.All(r => r.Links.Count == 0)) return;
            all = [.. _byTile.Values.SelectMany(l => l)];
        }
        _apply(all);
    }

    private static (int, float, float) Key(TileState tile) => (tile.SceneSno, MathF.Round(tile.OriginX), MathF.Round(tile.OriginY));

    // Only unrotated tiles, as PortalMemory: every DRLG tile seen so far is unrotated, and a wrong spot would mislead the guide.
    private List<TraversalMarker> InWorld(TileState tile, IReadOnlyList<TileActor> points) =>
        points.Count == 0 || TileRotation.QuarterTurns(tile.YawDegrees) != 0 ? []
        : [.. points.Select(p => new TraversalMarker(_kindOf(p.ActorSno), new Vector3(tile.OriginX, tile.OriginY, 0) + p.Local, p.Prefab))
            .Where(p => p.Kind != TraversalKind.None).Distinct()];

    /// <summary>A tile's links: each traversal prefab's own (<see cref="Traversal"/>), and the points placed outside one, or by a
    /// prefab of another shape, linked by distance (<see cref="Nearby"/>). Ends of neighbouring traversals can stand nearer than
    /// their own (the Temple of the Deathspeaker's chain of four jumps: 4.1 and 4.6 apart on one ledge, 4.9 across each gap).</summary>
    public static IReadOnlyList<JumpLink> Links(IReadOnlyList<TraversalMarker> points)
    {
        var links = new List<JumpLink>();
        var loose = points.Where(p => p.Prefab == 0).ToList();
        foreach (var prefab in points.Where(p => p.Prefab != 0).GroupBy(p => p.Prefab))
            if (Traversal([.. prefab]) is { } link) links.Add(link);
            else loose.AddRange(prefab);
        links.AddRange(Nearby(loose));
        return [.. links.Distinct()];
    }

    // One traversal prefab's link: between its two ends (Traversal_Jump_6m_Gizmo, _DuckUnder_2m_, _HandOverHand_10m_), one way from
    // its one jump point to the exit it lands at (Traversal_Jump_6m_OneWay) or from a slide to its foot, or a climb's top exit to its
    // bottom one; null for another shape.
    private static JumpLink? Traversal(IReadOnlyList<TraversalMarker> points)
    {
        List<Vector3> Of(TraversalKind kind) => [.. points.Where(p => p.Kind == kind).Select(p => p.At)];
        var (jumps, slides, downs, ups, exits) =
            (Of(TraversalKind.Jump), Of(TraversalKind.Slide), Of(TraversalKind.ClimbDown), Of(TraversalKind.ClimbUp), Of(TraversalKind.Exit));
        return (jumps.Count, slides.Count, downs.Count, ups.Count, exits.Count) switch
        {
            (2, 0, 0, 0, _) => new JumpLink(Flat(jumps[0]), Flat(jumps[1])),
            (1, 0, 0, 0, 1) => new JumpLink(Flat(jumps[0]), Flat(exits[0]), OneWay: true),
            (0, 1, 0, 0, 1) => new JumpLink(Flat(slides[0]), Flat(exits[0]), OneWay: true),
            (0, 0, 1, 1, 2) => Climb(downs[0], ups[0], exits),
            _ => null,
        };
    }

    // A climb's top exit (nearest the height of its way down) to its bottom one (nearest the height of its way up); null when one exit is both.
    private static JumpLink? Climb(Vector3 down, Vector3 up, IReadOnlyList<Vector3> exits)
    {
        var top = exits.MinBy(e => MathF.Abs(e.Z - down.Z));
        var bottom = exits.MinBy(e => MathF.Abs(e.Z - up.Z));
        return top == bottom ? null : new JumpLink(Flat(top), Flat(bottom));
    }

    private static (float X, float Y) Flat(Vector3 v) => (v.X, v.Y);

    /// <summary>Links by distance: jump points paired (<see cref="Pair"/>), each slide to its nearest exit one way, each climb's top
    /// exit to its bottom one (<see cref="Climb"/>, both within <see cref="ClimbExitUnits"/>); a climb with no such exits pairs as a
    /// jump point (a ladder's foot and top).</summary>
    private static List<JumpLink> Nearby(IReadOnlyList<TraversalMarker> points)
    {
        var exits = points.Where(p => p.Kind == TraversalKind.Exit).Select(p => p.At).ToList();
        var links = new List<JumpLink>();
        var paired = points.Where(p => p.Kind == TraversalKind.Jump).Select(p => p.At).ToList();
        foreach (var slide in points.Where(p => p.Kind == TraversalKind.Slide).Select(p => p.At))
            if (exits.Where(e => Distance(Flat(e), Flat(slide)) <= MaxPairUnits).OrderBy(e => Distance(Flat(e), Flat(slide))).FirstOrDefault() is var foot
                && exits.Contains(foot))
                links.Add(new JumpLink(Flat(slide), Flat(foot), OneWay: true));
        var climbed = new HashSet<Vector3>();
        foreach (var down in points.Where(p => p.Kind == TraversalKind.ClimbDown).Select(p => p.At))
        {
            if (points.Where(p => p.Kind == TraversalKind.ClimbUp && Distance(Flat(p.At), Flat(down)) <= 1f).Select(p => (Vector3?)p.At).FirstOrDefault() is not { } up)
                continue;
            var near = exits.Where(e => Distance(Flat(e), Flat(down)) <= ClimbExitUnits).ToList();
            if (near.Count < 2 || Climb(down, up, near) is not { } climb) continue;
            links.Add(climb);
            climbed.Add(down); climbed.Add(up);
        }
        paired.AddRange(points.Where(p => p.Kind is TraversalKind.ClimbUp or TraversalKind.ClimbDown && !climbed.Contains(p.At)).Select(p => p.At));
        links.AddRange(Pair([.. paired.Select(Flat).Distinct()]));
        return links;
    }

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
