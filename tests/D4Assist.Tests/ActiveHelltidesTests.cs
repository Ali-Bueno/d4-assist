using D4Assist.Game;

namespace D4Assist.Tests;

public class ActiveHelltidesTests
{
    // Live 2026-10-27: 4 with the helltide in Kehjistan, 3 in Hawezar, 2 in the Fractured Peaks; 0 in the five-minute gap.
    [Theory]
    [InlineData(1, HelltideRegion.Scosglen)]
    [InlineData(2, HelltideRegion.FracturedPeaks)]
    [InlineData(3, HelltideRegion.Hawezar)]
    [InlineData(4, HelltideRegion.Kehjistan)]
    [InlineData(5, HelltideRegion.DrySteppes)]
    public void UberSubzone_names_the_base_games_region(int value, HelltideRegion region) =>
        Assert.Equal([region], ActiveHelltides.From(new Dictionary<int, int> { [ActiveHelltides.UberSubzone] = value }, expansionsHaveHelltides: false));

    [Fact]
    public void The_expansions_have_their_own_state_beside_the_base_games()
    {
        var states = new Dictionary<int, int> { [ActiveHelltides.UberSubzone] = 3, [ActiveHelltides.UberSubzoneExpansions] = 2 };
        Assert.Equal([HelltideRegion.Hawezar, HelltideRegion.Skovos], ActiveHelltides.From(states, expansionsHaveHelltides: true));
        Assert.Equal([HelltideRegion.Nahantu], ActiveHelltides.From(new Dictionary<int, int> { [ActiveHelltides.UberSubzoneExpansions] = 1 }, expansionsHaveHelltides: true));
    }

    [Fact]
    public void Zero_an_unknown_value_or_no_state_is_no_helltide()
    {
        Assert.Empty(ActiveHelltides.From(new Dictionary<int, int> { [ActiveHelltides.UberSubzone] = 0 }, true));
        Assert.Empty(ActiveHelltides.From(new Dictionary<int, int> { [ActiveHelltides.UberSubzone] = 6, [ActiveHelltides.UberSubzoneExpansions] = 3 }, true));
        Assert.Empty(ActiveHelltides.From(new Dictionary<int, int>(), true));
    }

    // Live 2026-10-28, build 3.2.3: UberSubzone_Expansions read 1 beside UberSubzone 1 (Scosglen), with no helltide in Nahantu.
    [Fact]
    public void Before_patch_3_3_the_expansions_state_names_no_helltide()
    {
        var states = new Dictionary<int, int> { [ActiveHelltides.UberSubzone] = 1, [ActiveHelltides.UberSubzoneExpansions] = 1 };
        Assert.Equal([HelltideRegion.Scosglen], ActiveHelltides.From(states, ActiveHelltides.ExpansionsHaveHelltides("3.2.3.73883")));
        Assert.Equal([HelltideRegion.Scosglen, HelltideRegion.Nahantu], ActiveHelltides.From(states, ActiveHelltides.ExpansionsHaveHelltides("3.3.0.74145")));
    }

    [Fact]
    public void The_base_game_ends_on_the_hour_or_five_minutes_before_the_expansions_on_the_half_hour()
    {
        var now = new DateTime(2026, 10, 27, 18, 37, 10);
        Assert.Equal(23, ActiveHelltides.MinutesLeft(HelltideRegion.Hawezar, now, baseEndsEarly: false));
        Assert.Equal(18, ActiveHelltides.MinutesLeft(HelltideRegion.Hawezar, now, baseEndsEarly: true));
        Assert.Equal(53, ActiveHelltides.MinutesLeft(HelltideRegion.Nahantu, now, baseEndsEarly: true));
        Assert.Equal(60, ActiveHelltides.MinutesToNextBase(new DateTime(2026, 10, 27, 18, 0, 0)));
        Assert.Equal(1, ActiveHelltides.MinutesLeft(HelltideRegion.Skovos, new DateTime(2026, 10, 27, 18, 29, 30), baseEndsEarly: false));
    }

    [Theory]
    [InlineData("3.2.3.73883", true)]
    [InlineData("3.3.0.74145", false)]
    [InlineData(null, true)]
    public void Before_patch_3_3_a_base_helltide_ends_five_minutes_early(string? build, bool early) =>
        Assert.Equal(early, ActiveHelltides.BaseEndsEarly(build));
}
