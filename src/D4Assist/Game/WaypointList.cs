using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>One activated waypoint as the list speaks it: its name, its region (null when unknown), its spot, and how far and where
/// it lies from the player.</summary>
public sealed record WaypointEntry(string Name, string? Region, double X, double Y, double Distance, StickDirection Direction);

/// <summary>
/// The list of activated waypoints opened on the world map (Shift+F1): nearest first, moved through with up and down, which
/// stop at either end. Pure state; WaypointListBridge speaks it and moves the cursor.
/// </summary>
public sealed class WaypointList
{
    private IReadOnlyList<WaypointEntry> _entries = [];

    public bool IsOpen { get; private set; }

    /// <summary>The position of <see cref="Current"/>, 0-based.</summary>
    public int Index { get; private set; }

    public int Count => _entries.Count;

    /// <summary>Every entry, nearest to the player first; empty while closed.</summary>
    public IReadOnlyList<WaypointEntry> Entries => _entries;

    /// <summary>The entry the list stands on; null while closed.</summary>
    public WaypointEntry? Current => IsOpen ? _entries[Index] : null;

    /// <summary>The waypoints as entries, nearest to the player first.</summary>
    public static IReadOnlyList<WaypointEntry> Build(
        IEnumerable<(string Name, string? Region, double X, double Y)> waypoints, double playerX, double playerY) =>
        [.. waypoints
            .Select(w => new WaypointEntry(w.Name, w.Region, w.X, w.Y, Math.Sqrt(Math.Pow(w.X - playerX, 2) + Math.Pow(w.Y - playerY, 2)),
                ScreenDirections.Bin(w.X - playerX, w.Y - playerY)))
            .OrderBy(e => e.Distance).ThenBy(e => e.Name, StringComparer.CurrentCulture)];

    /// <summary>Opens on the first entry; an empty list stays closed.</summary>
    public void Open(IReadOnlyList<WaypointEntry> entries)
    {
        _entries = entries;
        Index = 0;
        IsOpen = entries.Count > 0;
    }

    public void Close()
    {
        IsOpen = false;
        _entries = [];
        Index = 0;
    }

    /// <summary>Moves to the next entry; false at the last one (the list stays there).</summary>
    public bool Next()
    {
        if (!IsOpen || Index >= _entries.Count - 1) return false;
        Index++;
        return true;
    }

    /// <summary>Moves to the previous entry; false at the first one.</summary>
    public bool Previous()
    {
        if (!IsOpen || Index == 0) return false;
        Index--;
        return true;
    }
}

/// <summary>A helltide object of the map's helltide list: its name, the level area it lies in and the helltide's area
/// ("Auen von Fethis, Hawezar"), in the current language; the last two null when unknown.</summary>
public sealed record HelltidePlace(string Name, string? Region, string? Area);
