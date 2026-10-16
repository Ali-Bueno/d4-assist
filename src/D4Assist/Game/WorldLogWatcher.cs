using System.Text;

namespace D4Assist.Game;

/// <summary>
/// Tails the game's FenrisDebug.txt for "Client entered world" lines (research/runtime-signals.md §2), so
/// the app learns world transitions without reading process memory. Opens the file with FileShare.ReadWrite
/// on every poll (the game keeps writing to it) and copes with truncation/rotation by restarting from the
/// top when the file has shrunk since the last poll.
/// </summary>
public sealed class WorldLogWatcher : IWorldNameSource, IDisposable
{
    public const string LogFileName = "FenrisDebug.txt";
    private const string EnteredWorldMarker = "[Game] Client entered world | world: ";
    private const string InstanceMarker = "| CWorldID: ";
    private const string CutsceneStateMarker = "[Game] ptCutscene->m_eCutsceneState: ";

    // World transitions are infrequent (dungeon entry/exit); no need to react faster than this.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private readonly Timer _timer;
    private readonly object _gate = new();
    private long _position;
    private bool _disposed;

    /// <summary>Raised once per "Client entered world" line seen after startup, with the world's name and instance.</summary>
    public event Action<WorldEntry>? WorldChanged;

    /// <summary>Raised once per "Current Text Locale" line seen after startup, with the locale (e.g. "enUS").</summary>
    public event Action<string>? TextLocaleChanged;

    /// <summary>Raised once per cutscene state line seen after startup (PRELOADING, PLAYBACK; research/runtime-signals.md §2).</summary>
    public event Action<string>? CutsceneStateChanged;

    public WorldLogWatcher(string installPath)
    {
        _path = Path.Combine(installPath, LogFileName);
        _position = File.Exists(_path) ? new FileInfo(_path).Length : 0;
        _timer = new Timer(_ => PollOnce(), null, PollInterval, PollInterval);
    }

    /// <summary>The last "Client entered world" line already in the file — the world to assume at startup.</summary>
    public WorldEntry? ReadLastWorldAtStartup() => ReadLastMatch(ExtractWorldEntry);

    /// <summary>The last "Current Text Locale" line already in the file — the game's language at startup.</summary>
    public string? ReadLastTextLocaleAtStartup() => ReadLastMatch(GameLocale.ExtractTextLocale);

    private T? ReadLastMatch<T>(Func<string, T?> extract) where T : class
    {
        if (!File.Exists(_path)) return null;
        T? last = null;
        foreach (var line in File.ReadLines(_path))
            if (extract(line) is { } match) last = match;
        return last;
    }

    /// <summary>Reads whatever new, complete lines exist since the last poll. Public so tests can drive it
    /// deterministically instead of waiting on the timer.</summary>
    public void PollOnce()
    {
        lock (_gate)
        {
            if (_disposed || !File.Exists(_path)) return;
            try
            {
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < _position) _position = 0; // truncated or rotated to a fresh file: start over
                int toRead = (int)(stream.Length - _position);
                if (toRead <= 0) return;

                stream.Seek(_position, SeekOrigin.Begin);
                var buffer = new byte[toRead];
                int read = stream.Read(buffer, 0, toRead);
                _position += ProcessCompleteLines(Encoding.UTF8.GetString(buffer, 0, read));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // file briefly locked while rotating; retried next poll
        }
    }

    // Runs on the timer thread, where an escaping exception ends the process; and a listener's IOException must not look like
    // a locked file to PollOnce, which would then replay the same lines on every poll. Each listener runs on its own.
    private static void Notify<T>(string feed, Action<T>? handlers, T value)
    {
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Action<T>>())
        {
            try { handler(value); }
            catch (Exception e) { Console.Error.WriteLine($"A {feed} listener failed on {value}: {e}"); }
        }
    }

    /// <summary>Only consumes bytes up to the last newline, so a line still being written is retried whole next poll.</summary>
    private int ProcessCompleteLines(string text)
    {
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0) return 0;
        string complete = text[..(lastNewline + 1)];
        foreach (var line in complete.Split('\n'))
        {
            if (GameLocale.ExtractTextLocale(line) is { } locale) Notify("text locale", TextLocaleChanged, locale);
            if (ExtractWorldEntry(line) is { } world) Notify("world", WorldChanged, world);
            if (ExtractCutsceneState(line) is { } state) Notify("cutscene", CutsceneStateChanged, state);
        }
        return Encoding.UTF8.GetByteCount(complete);
    }

    internal static string? ExtractWorldName(string line) => WordAfter(line, EnteredWorldMarker);

    internal static WorldEntry? ExtractWorldEntry(string line) =>
        ExtractWorldName(line) is { } name ? new WorldEntry(name, WordAfter(line, InstanceMarker)) : null;

    internal static string? ExtractCutsceneState(string line) => WordAfter(line, CutsceneStateMarker);

    private static string? WordAfter(string line, string marker)
    {
        int i = line.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return null;
        var rest = line[(i + marker.Length)..];
        int end = rest.IndexOfAny([' ', '\t', '\r', '\n']);
        return (end < 0 ? rest : rest[..end]).Trim();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
    }
}
