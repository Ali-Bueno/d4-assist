using D4Assist.Game;

namespace D4Assist.Tests;

public class SoulSplitLogTests
{
    [Fact]
    public void Logs_the_player_and_the_split_actors_from_the_first_poll_of_a_split()
    {
        // Live 2026-10-20: the first poll threw (TimeSpan overflow) and the log held only "began" and "ended".
        var feed = new FakeFeed();
        var lines = new List<string>();
        _ = new SoulSplitLog(feed, sno => sno == 1 ? "Rathma_SoulSplit_resurection_invisibleActor" : "Lorath", _ => null, lines.Add);
        feed.RaisePlayer(new PlayerState(-66, -1, 1, 0));

        feed.RaiseActors(new ActorState(5, 1, -67, -1, 1), new ActorState(6, 2, -60, 0, 1));

        Assert.Contains(lines, l => l.StartsWith("SOULSPLIT began"));
        Assert.Contains(lines, l => l.StartsWith("SOULSPLIT player (-66.0,-1.0,1.0)") && l.Contains("resurection_invisibleActor id=5"));
        Assert.Contains(lines, l => l.StartsWith("SOULSPLIT near: Lorath id=6"));
    }
}
