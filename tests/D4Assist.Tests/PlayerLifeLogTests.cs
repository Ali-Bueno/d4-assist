using D4Assist.Game;

namespace D4Assist.Tests;

public class PlayerLifeLogTests
{
    [Fact]
    public void Logs_the_first_read_then_only_changes_of_a_hundredth_or_more()
    {
        var lines = new List<string>();
        var log = new PlayerLifeLog(lines.Add);
        log.OnPlayer(new PlayerState(17.6f, 22.9f, 0, 0));

        log.OnLife(new PlayerLife(1000, 1000));
        log.OnLife(new PlayerLife(995, 1000)); // regeneration noise: under a hundredth
        log.OnLife(new PlayerLife(700, 1000));

        Assert.Equal(["LIFE 100% at (17.6,22.9)", "LIFE 70% -30 at (17.6,22.9)"], lines);
    }

    [Fact]
    public void Death_is_logged_once()
    {
        var lines = new List<string>();
        var log = new PlayerLifeLog(lines.Add);

        log.OnLife(new PlayerLife(20, 1000));
        log.OnLife(new PlayerLife(0, 1000));
        log.OnLife(new PlayerLife(0, 1000));

        Assert.Equal(["LIFE 2%", "LIFE 0% -2"], lines);
    }
}
