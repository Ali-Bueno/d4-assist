using D4Assist.Game;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class WaveStepDestinationTests
{
    // The Condemned Mines (QST_Frac_GaleValley_Main_05, log of 2026-11-02): the door Neyrelle opens, the waves' swarmer, the checkpoint.
    private static readonly (float X, float Y, float? Z) Door = (-153.9f, 48f, -8f);
    private const int Swarmer = 202811, Paladin = 2081670;

    private static MarkedActor Monster(float x, float y) => new(Swarmer, x, y, 0, 1, Kind: MapMarkerReader.KindBoss);
    private static MarkedActor Player(float x, float y) => new(Paladin, x, y, 0, 1, Kind: AwaitedAreaDestination.KindLocalPlayer);

    private TimeSpan _now;

    private WaveStepDestination Create() => new(clock: () => _now);

    [Fact]
    public void A_step_that_counts_waves_holds_the_last_quest_place_as_a_located_quest_objective()
    {
        var waves = Create();

        var place = Assert.Single(waves.Destinations(countsWaves: true, Door));

        Assert.Equal((Door.X, Door.Y), place.Position);
        Assert.Equal(ObjectiveMemory.QuestTier, place.Tier);
        Assert.Equal(WaveStepDestination.HoldUnits, place.Radius);
        Assert.True(place.Located);
    }

    [Fact]
    public void Another_step_holds_nothing_and_a_step_with_no_place_stood_at_neither()
    {
        var waves = Create();

        Assert.Empty(waves.Destinations(countsWaves: false, Door));
        Assert.Empty(waves.Destinations(countsWaves: true, null));
    }

    [Fact]
    public void The_place_stays_the_one_of_the_steps_start_and_ends_with_the_step()
    {
        var waves = Create();
        waves.Destinations(countsWaves: true, Door);

        var kept = Assert.Single(waves.Destinations(countsWaves: true, (0f, 0f, null)));
        Assert.Equal((Door.X, Door.Y), kept.Position);

        Assert.Empty(waves.Destinations(countsWaves: false, Door));
        var next = Assert.Single(waves.Destinations(countsWaves: true, (0f, 0f, null)));
        Assert.Equal((0f, 0f), next.Position);
    }

    [Fact]
    public void While_monsters_are_marked_at_the_place_it_holds_however_long_the_fight()
    {
        var waves = Create();
        waves.OnMarkers([Player(-150, 48), Monster(-140, 43)]);
        waves.Destinations(countsWaves: true, Door);

        _now += WaveStepDestination.QuietTime * 3;

        Assert.Single(waves.Destinations(countsWaves: true, Door));
    }

    [Fact]
    public void Nothing_coming_while_the_player_waits_there_lets_the_guide_go_on()
    {
        var waves = Create();
        waves.OnMarkers([Player(-150, 48)]);
        waves.Destinations(countsWaves: true, Door);

        _now += WaveStepDestination.QuietTime;

        Assert.Empty(waves.Destinations(countsWaves: true, Door));
    }

    [Fact]
    public void A_player_killed_and_sent_back_to_a_checkpoint_is_led_back_however_long_the_walk()
    {
        var waves = Create();
        waves.OnMarkers([Player(-113.5f, 29.5f)]);
        waves.Destinations(countsWaves: true, Door);

        _now += WaveStepDestination.QuietTime * 3;

        Assert.Single(waves.Destinations(countsWaves: true, Door));
    }

    [Fact]
    public void Monsters_marked_at_the_place_after_it_gave_way_call_the_guide_back()
    {
        var waves = Create();
        waves.OnMarkers([Player(-150, 48)]);
        waves.Destinations(countsWaves: true, Door);
        _now += WaveStepDestination.QuietTime;
        Assert.Empty(waves.Destinations(countsWaves: true, Door));

        waves.OnMarkers([Player(-150, 48), Monster(-140, 43)]);

        Assert.Single(waves.Destinations(countsWaves: true, Door));
    }

    [Fact]
    public void A_new_world_forgets_the_place()
    {
        var waves = Create();
        waves.Destinations(countsWaves: true, Door);

        waves.Reset();

        Assert.Empty(waves.Destinations(countsWaves: true, null));
    }
}
