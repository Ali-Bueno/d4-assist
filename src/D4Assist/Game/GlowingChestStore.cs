using System.Text.Json;

namespace D4Assist.Game;

/// <summary>
/// The chest kinds (actor SNOs) ever seen glowing, i.e. closed, kept in a file next to the exe: the glow is spawned by
/// the server, so no game data lists them, and a chest first seen already open is only judged once its kind is known.
/// </summary>
public sealed class GlowingChestStore
{
    public const string FileName = "d4assist-chests.json";

    private readonly string _path;
    private readonly Action<string>? _log;
    private readonly SortedSet<int> _kinds;

    private GlowingChestStore(string path, SortedSet<int> kinds, Action<string>? log)
    {
        _path = path;
        _kinds = kinds;
        _log = log;
    }

    public IReadOnlyCollection<int> Kinds => _kinds;

    /// <summary>Reads the file in <paramref name="directory"/>; a missing or unreadable file means no kinds yet.</summary>
    public static GlowingChestStore Load(string directory, Action<string>? log = null)
    {
        string path = Path.Combine(directory, FileName);
        var kinds = new SortedSet<int>();
        try
        {
            if (File.Exists(path)) kinds.UnionWith(JsonSerializer.Deserialize<int[]>(File.ReadAllText(path)) ?? []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            log?.Invoke($"{FileName} unreadable, starting empty: {e.Message}");
        }
        return new GlowingChestStore(path, kinds, log);
    }

    /// <summary>Records a newly learned kind and rewrites the file.</summary>
    public void Add(int actorSno)
    {
        if (!_kinds.Add(actorSno)) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_kinds)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"{FileName} not saved: {e.Message}");
        }
    }
}
