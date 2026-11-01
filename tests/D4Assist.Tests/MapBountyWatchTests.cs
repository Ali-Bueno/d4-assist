using D4Assist.Game;
using D4Memory.Ui;

namespace D4Assist.Tests;

public class MapBountyWatchTests
{
    private static readonly string[] MapOpen = [MenuScreens.MainMenu, MenuScreens.Map];

    private static MapBounties Held(int quest, params (int Quest, DateTime End)[] ends) =>
        new(new HashSet<int> { quest }, ends.ToDictionary(e => e.Quest, e => e.End));

    private sealed class Fixture
    {
        public int Reads;
        public MapBounties? Result = Held(7);
        public DateTime Now = new(2026, 11, 1, 14, 0, 0);
        public Action? Pending;
        public readonly MapBountyWatch Watch;
        public Fixture() => Watch = new MapBountyWatch(() => { Reads++; return Result; }, run: work => Pending = work, now: () => Now);
        public void Finish() { Pending?.Invoke(); Pending = null; }
    }

    [Fact]
    public void The_map_opening_starts_a_read_whose_pins_stay_until_it_closes()
    {
        var f = new Fixture();
        f.Watch.OnMenus([MenuScreens.MainMenu]);
        Assert.Null(f.Pending);

        f.Watch.OnMenus(MapOpen);
        Assert.Empty(f.Watch.Pins); // not done yet
        f.Finish();
        Assert.Contains(7, f.Watch.Pins);

        f.Watch.OnMenus([]);
        Assert.Empty(f.Watch.Pins);
    }

    [Fact]
    public void It_reads_again_only_after_the_interval_and_never_twice_at_once()
    {
        var f = new Fixture();
        f.Watch.OnMenus(MapOpen);
        var first = f.Pending;
        f.Now += MapBountyWatch.Interval * 2;
        f.Pending = null;
        f.Watch.OnMenus(MapOpen);
        Assert.Null(f.Pending); // the first still runs
        first!();
        f.Watch.OnMenus(MapOpen);
        Assert.NotNull(f.Pending);
        f.Finish();
        f.Watch.OnMenus(MapOpen);
        Assert.Null(f.Pending);
        Assert.Equal(2, f.Reads);
    }

    [Fact]
    public void A_read_ending_after_the_map_closed_is_dropped()
    {
        var f = new Fixture();
        f.Watch.OnMenus(MapOpen);
        f.Watch.OnMenus([]);
        f.Finish();
        Assert.Empty(f.Watch.Pins);
    }

    [Fact]
    public void Without_a_session_the_last_pins_stay()
    {
        var f = new Fixture();
        f.Watch.OnMenus(MapOpen);
        f.Finish();
        f.Result = null;
        f.Now += MapBountyWatch.Interval;
        f.Watch.OnMenus(MapOpen);
        f.Finish();
        Assert.Contains(7, f.Watch.Pins);
    }

    [Fact]
    public void The_minutes_left_round_up_and_end_with_the_bounty()
    {
        var f = new Fixture();
        f.Result = Held(7, (7, f.Now.AddMinutes(56)));
        f.Watch.OnMenus(MapOpen);
        f.Finish();

        Assert.Equal(56, f.Watch.MinutesLeft(7));
        f.Now = f.Now.AddSeconds(30);
        Assert.Equal(56, f.Watch.MinutesLeft(7));
        f.Now = f.Now.AddMinutes(55);
        Assert.Equal(1, f.Watch.MinutesLeft(7));
        f.Now = f.Now.AddSeconds(30);
        Assert.Null(f.Watch.MinutesLeft(7));
        Assert.Null(f.Watch.MinutesLeft(8)); // a legion: no end known
    }

    [Fact]
    public void An_end_stays_known_with_the_map_closed_and_a_later_read_updates_it()
    {
        var f = new Fixture();
        f.Result = Held(7, (7, f.Now.AddMinutes(30)));
        f.Watch.OnMenus(MapOpen);
        f.Finish();
        f.Watch.OnMenus([]);
        Assert.Equal(30, f.Watch.MinutesLeft(7));

        f.Result = Held(7, (7, f.Now.AddMinutes(90)));
        f.Watch.OnMenus(MapOpen);
        f.Finish();
        Assert.Equal(90, f.Watch.MinutesLeft(7));
    }
}
