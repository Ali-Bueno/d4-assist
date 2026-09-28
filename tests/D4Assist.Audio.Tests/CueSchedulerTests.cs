namespace D4Assist.Audio.Tests;

/// <summary>CueScheduler: D2R's no-overlap cue queue (d2r-access src/radar.h:64, radar_audio.cpp:129).</summary>
public class CueSchedulerTests
{
    private const double CueSeconds = 0.120;

    [Fact]
    public void FirstCue_StartsImmediately()
    {
        Assert.Equal(0.0, new CueScheduler().Reserve(nowSeconds: 5.0, CueSeconds));
    }

    [Fact]
    public void SimultaneousCues_PlayOneAfterAnotherWithTheD2RGap()
    {
        var scheduler = new CueScheduler();
        scheduler.Reserve(5.0, CueSeconds);
        var second = scheduler.Reserve(5.0, CueSeconds);
        var third = scheduler.Reserve(5.0, CueSeconds);

        Assert.Equal(CueSeconds + CueScheduler.CueGapSeconds, second!.Value, precision: 9);
        Assert.Equal(2 * (CueSeconds + CueScheduler.CueGapSeconds), third!.Value, precision: 9);
    }

    [Fact]
    public void CueAfterTheQueueDrained_StartsImmediately()
    {
        var scheduler = new CueScheduler();
        scheduler.Reserve(5.0, CueSeconds);
        Assert.Equal(0.0, scheduler.Reserve(6.0, CueSeconds));
    }

    [Fact]
    public void CueThatWouldStartTooLate_IsDropped()
    {
        var scheduler = new CueScheduler();
        var longCue = CueScheduler.MaxStartDelaySeconds + 0.5;
        scheduler.Reserve(5.0, longCue);
        Assert.Null(scheduler.Reserve(5.0, CueSeconds));
    }
}
