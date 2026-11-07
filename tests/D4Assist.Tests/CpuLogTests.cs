using D4Assist.Game;
using D4Memory.Scan;

namespace D4Assist.Tests;

public class CpuLogTests
{
    [Fact]
    public void The_line_gives_the_machine_share_and_each_polls_average()
    {
        var polls = new PollTimes();
        polls.Add("player", TimeSpan.FromMilliseconds(1));
        polls.Add("player", TimeSpan.FromMilliseconds(2));
        polls.Add("actors", TimeSpan.FromMilliseconds(40));

        string line = CpuLog.Line(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(60), 8, polls.Take());

        Assert.Equal("CPU: 2.5% of the machine over 60 s (8 processors); polls: actors 1 x 40.0 ms, player 2 x 1.5 ms.", line);
    }

    [Fact]
    public void Poll_times_start_over_once_taken()
    {
        var polls = new PollTimes();
        polls.Add("player", TimeSpan.FromMilliseconds(1));
        polls.Take();

        Assert.Empty(polls.Take());
    }

    [Fact]
    public void A_scan_runs_below_the_games_priority_and_gives_its_thread_back()
    {
        var regions = Enumerable.Range(1, 64).Select(i => new MemoryRegion(i * 0x1000L, 0x1000)).ToList();
        var seen = new System.Collections.Concurrent.ConcurrentBag<ThreadPriority>();

        BackgroundScan.ForEach(regions, (_, _, buffer) =>
        {
            Assert.Equal(RegionWindows.DefaultWindowBytes, buffer.Length);
            seen.Add(Thread.CurrentThread.Priority);
        });

        Assert.Equal(regions.Count, seen.Count);
        Assert.All(seen, priority => Assert.Equal(ThreadPriority.BelowNormal, priority));
        Assert.Equal(ThreadPriority.Normal, Thread.CurrentThread.Priority);
    }

    [Fact]
    public void A_scan_takes_at_most_a_quarter_of_the_machine()
    {
        Assert.InRange(BackgroundScan.Threads, 1, Math.Max(1, Environment.ProcessorCount / 4));
    }
}
