using System.Text.Json;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>What one dungeon instance's exploration looked like when last saved, and which instance of which game run it belongs to.</summary>
/// <param name="Visited">Explored tiles as [cellX, cellY] pairs.</param>
/// <param name="Objectives">The objectives heard and not done yet; null in a save from before 2026-10-04.</param>
/// <param name="Layers">Which tile carries each scene layer (SceneLayerPlacement); null in a save from before 2026-10-22.</param>
public sealed record ExplorationSnapshot(
    string World, string Instance, DateTime GameStartedUtc,
    IReadOnlyList<TileInput> Tiles, IReadOnlyList<int[]> Visited, IReadOnlyList<SavedPortal> Portals,
    IReadOnlyList<SavedObjective>? Objectives = null, IReadOnlyList<SavedLayer>? Layers = null);

/// <summary>The current dungeon's exploration, kept in one file next to the exe so a restart of the app finds it again.</summary>
public sealed class ExplorationStore(string directory, Action<string>? log = null)
{
    public const string FileName = "d4assist-exploration.json";

    private readonly string _path = Path.Combine(directory, FileName);
    private string? _written;

    /// <summary>The saved snapshot; null when there is none or it can't be read.</summary>
    public ExplorationSnapshot? Load()
    {
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<ExplorationSnapshot>(File.ReadAllText(_path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            log?.Invoke($"{FileName} unreadable, ignored: {e.Message}");
            return null;
        }
    }

    /// <summary>Rewrites the file when the snapshot changed since the last write.</summary>
    public void Save(ExplorationSnapshot snapshot)
    {
        string json = JsonSerializer.Serialize(snapshot);
        if (json == _written) return;
        try
        {
            File.WriteAllText(_path, json);
            _written = json;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"{FileName} not saved: {e.Message}");
        }
    }

    public void Delete()
    {
        _written = null;
        try { File.Delete(_path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"{FileName} not deleted: {e.Message}");
        }
    }
}
