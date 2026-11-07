using D4Assist.Audio;

namespace D4Assist.Audio.Tests;

public class PausingTimerTests
{
    private const int PeriodMs = 5;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public void It_ticks_only_while_running()
    {
        int ticks = 0;
        using var timer = new PausingTimer(() => Interlocked.Increment(ref ticks), PeriodMs);

        Thread.Sleep(PeriodMs * 6);
        Assert.Equal(0, Volatile.Read(ref ticks));

        timer.Run();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref ticks) >= 2, Wait));

        timer.Pause();
        Thread.Sleep(PeriodMs * 6); // a tick already on its way lands here
        int paused = Volatile.Read(ref ticks);
        Thread.Sleep(PeriodMs * 6);
        Assert.Equal(paused, Volatile.Read(ref ticks));

        timer.Run();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref ticks) > paused, Wait));
    }
}
