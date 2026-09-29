namespace D4Assist.Navigation;

/// <summary>
/// Finds a room's real openings from tile walkability instead of assuming the exit sits at the tile
/// edge's geometric midpoint (the room-announcement bug: on large tiles, or off-centre passages, the
/// midpoint can point straight into a wall). For each tile side with an exit bit, scans the boundary
/// row/column of that tile's local walkable grid for contiguous runs (dungeon-data.md §4.1: the
/// walkable box touches the tile edge exactly at every sampled single-exit tile, so depth 1 cell is
/// enough) and keeps runs wide enough for the player to fit through.
/// </summary>
public static class OpeningFinder
{
    /// <summary>Approximate player capsule radius, world units (task spec; unmeasured in research docs
    /// so far — research/d2r-radar-port.md §5 lists the D4 collision footprint as still missing).</summary>
    public const float PlayerCollisionRadius = 0.38f;

    /// <summary>An opening must be at least the player's diameter wide to be worth announcing.</summary>
    public const float MinOpeningWidthUnits = PlayerCollisionRadius * 2f;

    /// <summary>A run must span at least this many grid cells to count as a real passage — a single
    /// cell is grid/corner noise, not a passage (live log 2026-09-28: a 1-unit noise cell on the jail
    /// tile's West edge passed the width check yet was never a real opening; observed real D4 passages
    /// are 9-16 units wide).</summary>
    public const int MinOpeningCells = 2;

    public static List<RoomOpening> Find(DungeonMap map, IReadOnlyList<MapTile> members, HashSet<MapTile> memberSet, ITileWalkability? walkability)
    {
        var candidates = new List<((int, int) NeighbourCell, RoomOpening Opening)>();

        foreach (var tile in members)
            foreach (var side in ExitMaskBits.SidesOf(tile.Input.ExitMask))
            {
                var runs = TryFindRuns(tile, side, walkability);
                if (runs is null || runs.Count == 0)
                {
                    AddCandidate(map, memberSet, candidates, side,
                        ExitMaskBits.EdgeMidpoint(tile.Input, side),
                        side is ExitSide.North or ExitSide.South ? tile.Input.SizeY : tile.Input.SizeX,
                        usedFallback: true);
                    continue;
                }
                foreach (var (position, width) in runs)
                    AddCandidate(map, memberSet, candidates, side, position, width, usedFallback: false);
            }

        // Several runs can lead to the same neighbour cell (e.g. grid noise next to the real
        // passage); keep the widest one rather than whichever was found first.
        return candidates
            .GroupBy(c => c.NeighbourCell)
            .Select(g => g.OrderByDescending(c => c.Opening.Width).First().Opening)
            .ToList();
    }

    private static void AddCandidate(
        DungeonMap map, HashSet<MapTile> memberSet, List<((int, int) NeighbourCell, RoomOpening Opening)> candidates,
        ExitSide side, (float X, float Y) position, float width, bool usedFallback)
    {
        var (probeX, probeY) = Probe(side, position);
        var neighbour = map.TileAt(probeX, probeY);
        if (neighbour is not null && memberSet.Contains(neighbour)) return; // internal edge, not an opening

        var neighbourCell = (GridMath.CellIndex(probeX), GridMath.CellIndex(probeY));
        candidates.Add((neighbourCell, new RoomOpening(side, position, neighbour is null, width, usedFallback)));
    }

    /// <summary>A point half a cell beyond an opening, inside whatever lies past it.</summary>
    public static (float X, float Y) Probe(ExitSide side, (float X, float Y) position)
    {
        var (nx, ny) = ExitMaskBits.Normal(side);
        return (position.X + nx * (GridMath.CellSize / 2f), position.Y + ny * (GridMath.CellSize / 2f));
    }

    /// <summary>Null when walkability is unavailable or the tile's yaw isn't axis-aligned (fall back).</summary>
    private static List<((float X, float Y) Position, float Width)>? TryFindRuns(MapTile tile, ExitSide worldSide, ITileWalkability? walkability)
    {
        if (walkability is null || !walkability.TryGet(tile.Input.SceneSno, out var grid)) return null;
        var turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
        if (turns < 0) return null;

        var localSide = TileRotation.RotateSide(worldSide, (4 - turns) % 4);
        var runs = new List<((float X, float Y) Position, float Width)>();
        foreach (var (start, length) in BoundaryRuns(grid, tile, localSide))
        {
            if (length < MinOpeningCells) continue;
            if (length * grid.Resolution < MinOpeningWidthUnits) continue;
            var local = BoundaryPoint(grid, tile, localSide, start, length);
            var (rx, ry) = TileRotation.Rotate(local.X, local.Y, turns);
            runs.Add(((tile.Input.OriginX + rx, tile.Input.OriginY + ry), length * grid.Resolution));
        }
        return runs;
    }

    /// <summary>Contiguous walkable runs along the boundary of <paramref name="localSide"/>, as (start index, length).</summary>
    private static IEnumerable<(int Start, int Length)> BoundaryRuns(WalkGrid grid, MapTile tile, ExitSide localSide)
    {
        bool varyByColumn = localSide is ExitSide.North or ExitSide.South;
        int fixedIndex = varyByColumn
            ? BoundaryIndex(grid.OriginX, grid.Resolution, tile.Input.SizeX, localSide == ExitSide.North)
            : BoundaryIndex(grid.OriginY, grid.Resolution, tile.Input.SizeY, localSide == ExitSide.West);
        int count = varyByColumn ? grid.Cols : grid.Rows;

        int? runStart = null;
        for (var i = 0; i <= count; i++)
        {
            bool open = i < count && (varyByColumn ? grid.IsWalkable(fixedIndex, i) : grid.IsWalkable(i, fixedIndex));
            if (open && runStart is null) runStart = i;
            else if (!open && runStart is { } start) { yield return (start, i - start); runStart = null; }
        }
    }

    /// <summary>The grid row/column whose center sits half a cell inside the tile edge (dungeon-data.md
    /// §4.1: the walkable boundary touches the tile edge exactly, so this is the boundary cell).</summary>
    private static int BoundaryIndex(float gridOrigin, float resolution, float tileSize, bool nearZero) =>
        (int)MathF.Floor(((nearZero ? resolution / 2f : tileSize - resolution / 2f) - gridOrigin) / resolution);

    /// <summary>Local (x, y) point at the midpoint of a run, on the tile's own edge (not the grid's boundary cell).</summary>
    private static (float X, float Y) BoundaryPoint(WalkGrid grid, MapTile tile, ExitSide localSide, int start, int length)
    {
        var mid = start + length / 2f;
        return localSide switch
        {
            ExitSide.North => (0f, grid.OriginY + mid * grid.Resolution),
            ExitSide.South => (tile.Input.SizeX, grid.OriginY + mid * grid.Resolution),
            ExitSide.West => (grid.OriginX + mid * grid.Resolution, 0f),
            ExitSide.East => (grid.OriginX + mid * grid.Resolution, tile.Input.SizeY),
            _ => throw new ArgumentOutOfRangeException(nameof(localSide)),
        };
    }
}
