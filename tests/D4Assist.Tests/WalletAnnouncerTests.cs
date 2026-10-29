using D4Assist.Game;
using D4Assist.Strings;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class WalletAnnouncerTests
{
    private static readonly Func<int, string?> GameNames = type => type switch
    {
        CurrencyReader.Cinders => "Glut",
        CurrencyReader.BanefulHearts => "Unheilsherz",
        _ => null,
    };

    private static long[] Amounts(long cinders, long hearts)
    {
        var amounts = new long[CurrencyReader.Count];
        amounts[0] = 93_051_377; // gold, never said
        amounts[CurrencyReader.Cinders] = cinders;
        amounts[CurrencyReader.BanefulHearts] = hearts;
        return amounts;
    }

    [Fact]
    public void Both_counts_are_said_by_the_games_names()
    {
        // Live 2026-10-28 in a helltide: 36 cinders, one Baneful Heart.
        Assert.Equal("Glut: 36, Unheilsherz: 1", WalletAnnouncer.Text(Amounts(36, 1), GameNames, WalletStrings.German));
    }

    [Fact]
    public void An_amount_of_zero_is_said_as_none()
    {
        // After the helltide (before patch 3.3.0) the cinders are gone, the hearts stay.
        Assert.Equal("Glut: keine, Unheilsherz: 2", WalletAnnouncer.Text(Amounts(0, 2), GameNames, WalletStrings.German));
        Assert.Equal("Aberrant Cinders: none, Baneful Heart: none", WalletAnnouncer.Text(Amounts(0, 0), _ => null, WalletStrings.English));
    }

    [Fact]
    public void Unreadable_amounts_say_so()
    {
        Assert.Equal(WalletStrings.German.Unreadable, WalletAnnouncer.Text(null, GameNames, WalletStrings.German));
    }
}
