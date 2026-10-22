using System.Text.Json;

namespace D4Assist.Game;

/// <summary>The last phase read of each quest, in any world since the app started: in a dungeon the game may hold another quest's
/// pair the whole time (live 2026-10-17: QST_Frac_PaleKnightsKin read at phase 15, bring the journals back, in the overworld, and
/// never in the Light's Watch). Shared by every world session (ActiveQuests); thread-safe.</summary>
/// <remarks>Loaded from a file next to the exe (<see cref="Load"/>), it also keeps the phases of earlier runs: the module data holds
/// one quest's pair at a time, and after an app restart "Verwundete Seelen" was never found in progress again, its pair taken by a
/// collectible quest nearby, so D-pad left named none of its markers (live 2026-10-21). A phase not read for
/// <see cref="KeepDays"/> days is dropped.</remarks>
public sealed class QuestPhaseHistory
{
    public const string FileName = "d4assist-quests.json";

    /// <summary>A quest whose phase wasn't read for this long is forgotten (it was finished or abandoned meanwhile).</summary>
    public const int KeepDays = 30;

    private readonly object _gate = new();
    private readonly Dictionary<int, Entry> _last = [];
    private readonly string? _path;
    private readonly Func<DateTime> _clock;
    private readonly Action<string>? _log;

    /// <summary>One quest's last phase and when it was last read (UTC).</summary>
    public sealed record Entry(uint Phase, DateTime Read);

    // A phase read again rewrites the file at most this often (the phases are read on every poll).
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);

    /// <summary>A history of this run only, kept in no file.</summary>
    public QuestPhaseHistory() : this(null, null, null) { }

    private QuestPhaseHistory(string? path, Func<DateTime>? clock, Action<string>? log)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.UtcNow);
        _log = log;
    }

    /// <summary>Reads the file in <paramref name="directory"/>, dropping phases older than <see cref="KeepDays"/>; a missing or
    /// unreadable file means no phases yet. Each new phase rewrites it, and a phase read again at most hourly.</summary>
    /// <param name="clock">UTC now; null for the system clock.</param>
    public static QuestPhaseHistory Load(string directory, Func<DateTime>? clock = null, Action<string>? log = null)
    {
        var history = new QuestPhaseHistory(Path.Combine(directory, FileName), clock, log);
        try
        {
            if (File.Exists(history._path))
            {
                var cutoff = history._clock() - TimeSpan.FromDays(KeepDays);
                foreach (var (quest, entry) in JsonSerializer.Deserialize<Dictionary<int, Entry>>(File.ReadAllText(history._path!)) ?? [])
                    if (entry is not null && entry.Read >= cutoff) history._last[quest] = entry;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            log?.Invoke($"{FileName} unreadable, starting empty: {e.Message}");
        }
        return history;
    }

    public void Record(int quest, uint phase)
    {
        lock (_gate)
        {
            var now = _clock();
            if (_last.TryGetValue(quest, out var known) && known.Phase == phase && now - known.Read < RefreshInterval) return;
            _last[quest] = new Entry(phase, now);
            Save();
        }
    }

    /// <summary>Null while the quest's phase was never read.</summary>
    public uint? Last(int quest)
    {
        lock (_gate) return _last.TryGetValue(quest, out var entry) ? entry.Phase : null;
    }

    // Under the gate.
    private void Save()
    {
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(new SortedDictionary<int, Entry>(_last))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"{FileName} not saved: {e.Message}");
        }
    }
}
