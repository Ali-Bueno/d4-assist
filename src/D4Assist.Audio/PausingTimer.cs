namespace D4Assist.Audio;

/// <summary>A periodic timer that stands still with nothing to do: an idle cue's ticks only wake pool threads. Not thread-safe, callers hold their lock.</summary>
public sealed class PausingTimer(Action tick, int periodMs) : IDisposable
{
    private readonly Timer _timer = new(_ => tick(), null, Timeout.Infinite, Timeout.Infinite);
    private bool _running;

    public void Run()
    {
        if (_running) return;
        _running = true;
        _timer.Change(periodMs, periodMs);
    }

    public void Pause()
    {
        if (!_running) return;
        _running = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose() => _timer.Dispose();
}
