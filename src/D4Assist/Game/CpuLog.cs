using System.Globalization;

namespace D4Assist.Game;

/// <summary>How long each poll ran since last asked; the polls add to it from their own timers.</summary>
public sealed class PollTimes
{
    private readonly object _gate = new();
    private Dictionary<string, (int Count, TimeSpan Total)> _polls = [];

    public void Add(string poll, TimeSpan took)
    {
        lock (_gate)
        {
            var (count, total) = _polls.GetValueOrDefault(poll);
            _polls[poll] = (count + 1, total + took);
        }
    }

    /// <summary>What ran since the last call.</summary>
    public IReadOnlyDictionary<string, (int Count, TimeSpan Total)> Take()
    {
        lock (_gate)
        {
            var taken = _polls;
            _polls = [];
            return taken;
        }
    }
}

/// <summary>Logs once a minute of play how much of the machine the app took and what its polls cost, so a player's log shows what
/// the app itself does to a loaded PC.</summary>
/// <param name="playing">True while a memory session is open; idle time isn't logged.</param>
public sealed class CpuLog(PollTimes polls, Func<bool> playing, Action<string> log) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private Timer? _timer;
    private TimeSpan _cpu;
    private long _since;

    public void Start()
    {
        (_cpu, _since) = (Environment.CpuUsage.TotalTime, TimeProvider.System.GetTimestamp());
        _timer = new Timer(_ => Tick(), null, Interval, Interval);
    }

    private void Tick()
    {
        lock (_gate)
        {
            var cpu = Environment.CpuUsage.TotalTime;
            var elapsed = TimeProvider.System.GetElapsedTime(_since);
            string line = Line(cpu - _cpu, elapsed, Environment.ProcessorCount, polls.Take());
            (_cpu, _since) = (cpu, TimeProvider.System.GetTimestamp());
            if (playing()) log(line);
        }
    }

    /// <summary>The log line for <paramref name="cpu"/> taken over <paramref name="elapsed"/>.</summary>
    public static string Line(TimeSpan cpu, TimeSpan elapsed, int processors, IReadOnlyDictionary<string, (int Count, TimeSpan Total)> polls)
    {
        double share = cpu / (elapsed * processors) * 100;
        var costs = polls.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.Key} {p.Value.Count} x {p.Value.Total.TotalMilliseconds / p.Value.Count:F1} ms"));
        return string.Create(CultureInfo.InvariantCulture,
            $"CPU: {share:F1}% of the machine over {elapsed.TotalSeconds:F0} s ({processors} processors); polls: {string.Join(", ", costs)}.");
    }

    public void Dispose() => _timer?.Dispose();
}
