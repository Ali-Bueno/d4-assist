using System.Diagnostics;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Reads the watched menu screens' open flags each poll; locates their records with a background scan, once per
/// game process, and again only when a record stops validating (a character switch rebuilds the UI).
/// </summary>
public sealed class MenuScreenTracker(Func<IMemoryReader> openScanReader, Func<TimeSpan> clock, Action<string>? log = null)
{
    // Wait after a scan before the next one. The screens appear seconds after a world loads, so the first retry is soon;
    // each miss doubles the wait up to the cap, so a layout that no longer matches (game update) doesn't keep the cores busy.
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(60);

    private const string ScreenPrefix = "screen_";

    private readonly object _gate = new();
    private Dictionary<string, long> _records = [];
    private ModuleRange _module;
    private bool _scanning, _needsScan = true;
    private TimeSpan _nextScanAt, _retryDelay = FirstRetryDelay;

    /// <summary>The scan opens its own handle so it outlives the world session that triggered it.</summary>
    public static MenuScreenTracker ForLiveProcess(Action<string>? log = null)
    {
        var watch = Stopwatch.StartNew();
        return new MenuScreenTracker(() => ProcessReader.OpenDiabloIv(), () => watch.Elapsed, log);
    }

    /// <summary>Names of the open watched screens; null while the records are not located or one just failed validation.</summary>
    public IReadOnlyList<string>? Read(IMemoryReader memory, ModuleRange module)
    {
        Dictionary<string, long> records;
        lock (_gate)
        {
            if (module != _module) // new game process
                (_module, _records, _needsScan, _nextScanAt, _retryDelay) = (module, [], true, TimeSpan.Zero, FirstRetryDelay);
            records = _records;
        }

        var open = new List<string>();
        var invalid = new List<string>();
        foreach (var (name, record) in records)
        {
            if (ScreenRegistryReader.TryReadOpen(memory, record, module) is not { } isOpen) invalid.Add(name);
            else if (isOpen) open.Add(name);
        }

        lock (_gate)
        {
            foreach (var name in invalid) _records.Remove(name);
            if (invalid.Count > 0) _needsScan = true;
            TryStartScanLocked();
        }
        return records.Count == 0 || invalid.Count > 0 ? null : open;
    }

    private void TryStartScanLocked()
    {
        var now = clock();
        if (!_needsScan || _scanning || now < _nextScanAt) return;
        _scanning = true;
        _needsScan = false;
        var module = _module;
        var missing = MenuScreens.Watched.Where(n => !_records.ContainsKey(n)).ToList();
        Task.Run(() => Scan(missing, module));
    }

    private void Scan(List<string> names, ModuleRange module)
    {
        var started = clock();
        Dictionary<string, long> found = [];
        try
        {
            var reader = openScanReader();
            try { found = ScreenRegistryReader.Locate(reader, names, module); }
            finally { (reader as IDisposable)?.Dispose(); }
        }
        catch (InvalidOperationException) { } // game process not running; retried on the next failure
        catch (Exception e) { log?.Invoke($"MENU scan failed: {e.Message}"); }

        lock (_gate)
        {
            _scanning = false;
            if (module != _module) return; // the process changed while scanning
            var now = clock();
            if (found.Count > 0) (_nextScanAt, _retryDelay) = (now + FirstRetryDelay, FirstRetryDelay);
            else (_nextScanAt, _retryDelay) = (now + _retryDelay, TimeSpan.FromTicks(Math.Min(_retryDelay.Ticks * 2, MaxRetryDelay.Ticks)));
            foreach (var (name, record) in found) _records[name] = record;
            _needsScan = _records.Count == 0; // a partial result is kept; only an empty one retries
        }
        var missing = names.Where(n => !found.ContainsKey(n)).Select(n => n.Replace(ScreenPrefix, "")).ToList();
        log?.Invoke($"MENU located {found.Count}/{names.Count} screens in {(clock() - started).TotalMilliseconds:F0} ms" +
            (missing.Count > 0 ? $"; missing {string.Join(" ", missing)}" : ""));
    }
}
