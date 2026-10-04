using System.Diagnostics;
using D4Memory.Actors;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Assist.Game;

/// <summary>
/// Reads the client's map-marker list on every actor poll; finds its head with a background scan, once per game process and
/// again only after the head failed to validate for a while (research/dungeon-objectives.md §19).
/// </summary>
public sealed class MapMarkerTracker(Func<IMemoryReader> openScanReader, Func<TimeSpan> clock, Action<string>? log = null)
{
    /// <summary>Failed reads in a row before the head is searched again: the game rebuilds the list in place (live 2026-10-04).</summary>
    public const int RescanAfterFailures = 10;

    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private long _moduleBase;
    private long? _head;
    private int _failures;
    private bool _scanning;
    private TimeSpan _nextScanAt, _retryDelay = FirstRetryDelay;

    /// <summary>The scan opens its own handle so it outlives the world session that triggered it.</summary>
    public static MapMarkerTracker ForLiveProcess(Action<string>? log = null)
    {
        var watch = Stopwatch.StartNew();
        return new MapMarkerTracker(() => ProcessReader.OpenDiabloIv(), () => watch.Elapsed, log);
    }

    /// <summary>The markers now; null while the head isn't found or the list doesn't read.</summary>
    public IReadOnlyList<MapMarker>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        long? head;
        lock (_gate)
        {
            if (moduleBase != _moduleBase) // new game process
                (_moduleBase, _head, _failures, _nextScanAt, _retryDelay) = (moduleBase, null, 0, TimeSpan.Zero, FirstRetryDelay);
            head = _head;
        }
        var markers = head is { } at ? MapMarkerReader.TryRead(memory, at) : null;
        lock (_gate)
        {
            if (markers is not null) _failures = 0;
            else if (head is not null && ++_failures >= RescanAfterFailures && _head == head) (_head, _failures) = (null, 0);
            if (_head is null) TryStartScanLocked(gameObjectPointerRva);
        }
        return markers;
    }

    private void TryStartScanLocked(long gameObjectPointerRva)
    {
        if (_scanning || clock() < _nextScanAt) return;
        _scanning = true;
        long moduleBase = _moduleBase;
        Task.Run(() => Scan(moduleBase, gameObjectPointerRva));
    }

    private void Scan(long moduleBase, long gameObjectPointerRva)
    {
        var started = clock();
        long? head = null;
        try
        {
            var reader = openScanReader();
            try { head = MapMarkerReader.Locate(reader, moduleBase, gameObjectPointerRva); }
            finally { (reader as IDisposable)?.Dispose(); }
        }
        catch (InvalidOperationException) { } // game process not running; retried on the next read
        catch (Exception e) { log?.Invoke($"MARKERS scan failed: {e.Message}"); }

        lock (_gate)
        {
            _scanning = false;
            if (moduleBase != _moduleBase) return; // the process changed while scanning
            var now = clock();
            if (head is not null) (_head, _failures, _nextScanAt, _retryDelay) = (head, 0, now + FirstRetryDelay, FirstRetryDelay);
            else (_nextScanAt, _retryDelay) = (now + _retryDelay, TimeSpan.FromTicks(Math.Min(_retryDelay.Ticks * 2, MaxRetryDelay.Ticks)));
        }
        log?.Invoke(head is { } found
            ? $"MARKERS list at 0x{found:X} ({(clock() - started).TotalMilliseconds:F0} ms)"
            : $"MARKERS list not found ({(clock() - started).TotalMilliseconds:F0} ms)");
    }
}
