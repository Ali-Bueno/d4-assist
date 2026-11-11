using System.Numerics;
using System.Text.Json;

namespace D4Assist.Game;

/// <summary>
/// The spots where the player stood stuck on the overworld guide's path (QuestPathBridge), by world: the static surfaces call floor
/// what the game blocks there (the Altar of Ruin's east gap, 2026-11-11). Kept in a file next to the exe, so the next run's path
/// goes round at once instead of after the player stood stuck again (user request 2026-11-11). A spot the player walks through
/// opens again; one not confirmed for <see cref="KeepDays"/> days is dropped (a game update may have opened it). Thread-safe.
/// </summary>
public sealed class BlockedSpots
{
    public const string FileName = "d4assist-blocked.json";

    /// <summary>A spot learned this long ago is forgotten.</summary>
    public const int KeepDays = 60;

    /// <summary>One blocked spot: its 1-unit cells at the height of the player's feet, where the player stood stuck, and when (UTC).</summary>
    public sealed record Spot(List<int[]> Cells, float Z, float StuckX, float StuckY, DateTime Learned)
    {
        internal DoorWall Wall() => new(Cells.Select(c => (c[0], c[1])).ToHashSet(), Z);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, List<Spot>> _spots = [];
    private readonly Dictionary<string, IReadOnlyList<DoorWall>> _walls = [];
    private readonly string? _path;
    private readonly Func<DateTime> _clock;
    private readonly Action<string>? _log;

    /// <summary>Spots of this run only, kept in no file.</summary>
    public BlockedSpots() : this(null, null, null) { }

    private BlockedSpots(string? path, Func<DateTime>? clock, Action<string>? log)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.UtcNow);
        _log = log;
    }

    /// <summary>Reads the file in <paramref name="directory"/>, dropping spots older than <see cref="KeepDays"/>; a missing or
    /// unreadable file means no spots yet. Every change rewrites it.</summary>
    public static BlockedSpots Load(string directory, Func<DateTime>? clock = null, Action<string>? log = null)
    {
        var spots = new BlockedSpots(Path.Combine(directory, FileName), clock, log);
        try
        {
            if (File.Exists(spots._path))
            {
                var cutoff = spots._clock() - TimeSpan.FromDays(KeepDays);
                foreach (var (world, list) in JsonSerializer.Deserialize<Dictionary<string, List<Spot>>>(File.ReadAllText(spots._path!)) ?? [])
                {
                    var kept = (list ?? []).Where(s => s is { Cells: not null } && s.Learned >= cutoff && s.Cells.All(c => c is { Length: 2 })).ToList();
                    if (kept.Count > 0) spots._spots[world] = kept;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            log?.Invoke($"{FileName} unreadable, starting empty: {e.Message}");
        }
        return spots;
    }

    /// <summary>The world's blocked spots as walls for the path search.</summary>
    public IReadOnlyList<DoorWall> Walls(string world)
    {
        lock (_gate)
        {
            if (_walls.TryGetValue(world, out var known)) return known;
            return _walls[world] = _spots.TryGetValue(world, out var list) ? [.. list.Select(s => s.Wall())] : [];
        }
    }

    public int Count(string world)
    {
        lock (_gate) return _spots.TryGetValue(world, out var list) ? list.Count : 0;
    }

    public void Add(string world, IReadOnlySet<(int X, int Y)> cells, float z, Vector2 stuckAt)
    {
        lock (_gate)
        {
            if (!_spots.TryGetValue(world, out var list)) _spots[world] = list = [];
            list.Add(new Spot([.. cells.Select(c => new[] { c.X, c.Y })], z, stuckAt.X, stuckAt.Y, _clock()));
            Changed(world);
        }
    }

    /// <summary>Opens the spots the player stands in, at least <paramref name="pastStuckUnits"/> past where they stood stuck (nearer,
    /// they only sway there); how many opened.</summary>
    public int OpenWalkedThrough(string world, Vector3 player, float pastStuckUnits, float floorUnits)
    {
        lock (_gate)
        {
            if (!_spots.TryGetValue(world, out var list) || list.Count == 0) return 0;
            int x = (int)MathF.Floor(player.X), y = (int)MathF.Floor(player.Y);
            var here = new Vector2(player.X, player.Y);
            int removed = list.RemoveAll(s => MathF.Abs(s.Z - player.Z) <= floorUnits
                && Vector2.Distance(here, new Vector2(s.StuckX, s.StuckY)) >= pastStuckUnits
                && s.Cells.Exists(c => c[0] == x && c[1] == y));
            if (removed > 0) Changed(world);
            return removed;
        }
    }

    // Under the gate.
    private void Changed(string world)
    {
        _walls.Remove(world);
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_spots.Where(w => w.Value.Count > 0).ToDictionary(w => w.Key, w => w.Value))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"{FileName} not saved: {e.Message}");
        }
    }
}
