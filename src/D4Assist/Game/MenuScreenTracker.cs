using System.Diagnostics;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Reads the watched menu screens' open flags each poll; locates their records with a background scan, once per
/// game process, again when a record stops validating, and on every world change (<see cref="Relocate"/>).
/// A record counts as open only after it has been seen closed: a real menu toggles its record, while a stale one left by a
/// character switch reads open from the moment it is located (live 2026-10-04: <c>screen_Hall_Of_Fallen_Heroes</c> for
/// 6 minutes, muting every cue). Such a record never mutes, and a scan that skips it looks for the screen's live record at once.
/// </summary>
public sealed class MenuScreenTracker(Func<IMemoryReader> openScanReader, Func<TimeSpan> clock, Action<string>? log = null)
{
    // Wait after a scan before the next one. The screens appear seconds after a world loads, so the first retry is soon;
    // each miss doubles the wait up to the cap, so a layout that no longer matches (game update) doesn't keep the cores busy.
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>A screen open this long gets the records checked once against a fresh scan: the last resort for a record that
    /// went stale after it was seen closed, which no live case has shown. At worst a false menu mutes the cues this long.</summary>
    public static readonly TimeSpan VerifyOpenAfter = TimeSpan.FromSeconds(15);

    private const string ScreenPrefix = "screen_";

    private readonly object _gate = new();
    private Dictionary<string, long> _records = [];
    private ModuleRange _module;
    private bool _scanning, _needsScan = true;
    private TimeSpan _nextScanAt, _retryDelay = FirstRetryDelay;
    private int _generation;
    private TimeSpan? _openSince;
    private bool _openVerified;
    // Records seen closed since the last world change: only these count as open.
    private HashSet<long> _proven = [];
    // Records read open before ever closing: scans skip them. A screen in _suspectNames is looked for again without waiting.
    private HashSet<long> _suspects = [];
    private HashSet<string> _suspectNames = [];

    /// <summary>The scan opens its own handle so it outlives the world session that triggered it.</summary>
    public static MenuScreenTracker ForLiveProcess(Action<string>? log = null)
    {
        var watch = Stopwatch.StartNew();
        return new MenuScreenTracker(() => ProcessReader.OpenDiabloIv(), () => watch.Elapsed, log);
    }

    /// <summary>Drops every record and locates them again: a character switch rebuilds the UI, and an old record can still
    /// validate with its open byte at 1, which muted every cue until the app restarted (live 2026-10-04).</summary>
    public void Relocate()
    {
        lock (_gate)
        {
            (_records, _needsScan, _nextScanAt, _retryDelay, _generation, _openSince, _openVerified) =
                ([], true, TimeSpan.Zero, FirstRetryDelay, _generation + 1, null, false);
            (_proven, _suspects, _suspectNames) = ([], [], []);
        }
    }

    /// <summary>Names of the open watched screens; null while the records are not located or one just failed validation.</summary>
    public IReadOnlyList<string>? Read(IMemoryReader memory, ModuleRange module)
    {
        List<KeyValuePair<string, long>> records;
        lock (_gate)
        {
            if (module != _module) // new game process
            {
                (_module, _records, _needsScan, _nextScanAt, _retryDelay) = (module, [], true, TimeSpan.Zero, FirstRetryDelay);
                (_proven, _suspects, _suspectNames) = ([], [], []);
            }
            records = [.. _records]; // a finishing scan adds to the dictionary
        }

        var reads = records.Select(r => (Name: r.Key, Record: r.Value, Open: ScreenRegistryReader.TryReadOpen(memory, r.Value, module)))
            .ToList();

        lock (_gate)
        {
            var open = new List<string>();
            bool invalid = false;
            foreach (var (name, record, isOpen) in reads)
            {
                if (isOpen is not { } flag)
                {
                    invalid = true;
                    if (_records.TryGetValue(name, out var current) && current == record) _records.Remove(name);
                }
                else if (!flag)
                {
                    _proven.Add(record);
                    _suspects.Remove(record); // it was a real menu, open when located
                }
                else if (_proven.Contains(record)) open.Add(name);
                else if (_suspects.Add(record))
                {
                    _suspectNames.Add(name);
                    log?.Invoke($"MENU {Short(name)} reads open but was never seen closed: not muting, looking for its live record");
                }
            }
            if (invalid) _needsScan = true;
            if (records.Count > 0 && !invalid) VerifyLongOpenLocked(open.Count > 0);
            TryStartScanLocked();
            return records.Count == 0 || invalid ? null : open;
        }
    }

    // Once per stretch of open menus: a menu really open is found open again; a stale record is replaced by the live one.
    // Records already seen closed keep counting, so a real menu stays muted through the check.
    private void VerifyLongOpenLocked(bool anyOpen)
    {
        if (!anyOpen) { (_openSince, _openVerified) = (null, false); return; }
        var now = clock();
        _openSince ??= now;
        if (_openVerified || now - _openSince < VerifyOpenAfter) return;
        _openVerified = true;
        log?.Invoke($"MENU open for {VerifyOpenAfter.TotalSeconds:F0} s: locating the screens again");
        (_records, _needsScan, _nextScanAt, _retryDelay, _generation) = ([], true, TimeSpan.Zero, FirstRetryDelay, _generation + 1);
    }

    private void TryStartScanLocked()
    {
        if (_scanning) return;
        bool missingDue = _needsScan && clock() >= _nextScanAt;
        if (!missingDue && _suspectNames.Count == 0) return;
        var missing = missingDue ? MenuScreens.Watched.Where(n => !_records.ContainsKey(n)) : [];
        var names = missing.Union(_suspectNames).ToList();
        _scanning = true;
        if (missingDue) _needsScan = false;
        _suspectNames = [];
        var (module, generation, exclude) = (_module, _generation, _suspects.ToHashSet());
        Task.Run(() => Scan(names, exclude, missingDue, module, generation));
    }

    private void Scan(List<string> names, HashSet<long> exclude, bool missingScan, ModuleRange module, int generation)
    {
        var started = clock();
        Dictionary<string, long> found = [];
        try
        {
            var reader = openScanReader();
            try { found = ScreenRegistryReader.Locate(reader, names, module, exclude); }
            finally { (reader as IDisposable)?.Dispose(); }
        }
        catch (InvalidOperationException) { } // game process not running; retried on the next failure
        catch (Exception e) { log?.Invoke($"MENU scan failed: {e.Message}"); }

        lock (_gate)
        {
            _scanning = false;
            if (module != _module) return; // the process changed while scanning
            if (generation != _generation) { _needsScan = true; return; } // relocated meanwhile: these records may be the old ones
            if (missingScan) // a search for a suspect's live record alone leaves the retry pace alone
            {
                var now = clock();
                if (found.Count > 0) (_nextScanAt, _retryDelay) = (now + FirstRetryDelay, FirstRetryDelay);
                else (_nextScanAt, _retryDelay) = (now + _retryDelay, TimeSpan.FromTicks(Math.Min(_retryDelay.Ticks * 2, MaxRetryDelay.Ticks)));
            }
            foreach (var (name, record) in found) _records[name] = record; // a suspect found no other record: kept, it counts once closed
            if (_records.Count == 0) _needsScan = true; // a partial result is kept; only an empty one retries
        }
        var missing = names.Where(n => !found.ContainsKey(n)).Select(Short).ToList();
        log?.Invoke($"MENU located {found.Count}/{names.Count} screens in {(clock() - started).TotalMilliseconds:F0} ms" +
            (missing.Count > 0 ? $"; missing {string.Join(" ", missing)}" : ""));
    }

    private static string Short(string name) => name.Replace(ScreenPrefix, "");
}
