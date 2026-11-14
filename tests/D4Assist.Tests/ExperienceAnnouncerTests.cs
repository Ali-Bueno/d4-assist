using D4Assist.Game;
using D4Assist.Navigation;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class ExperienceAnnouncerTests
{
    private static string Text(PlayerExperience? read) => ExperienceAnnouncer.Text(read, ExperienceStrings.English);

    [Fact]
    public void Below_the_cap_it_says_the_level_and_what_is_left_to_the_next() =>
        Assert.Equal("Level 37. 5123456789 experience to the next level.", Text(new PlayerExperience(37, 5_123_456_789, 0, 0)));

    [Fact]
    public void At_the_cap_it_says_the_paragon_level_and_what_is_left_to_the_next() =>
        Assert.Equal("Level 60, paragon 12. 345678 experience to the next paragon level.", Text(new PlayerExperience(60, 0, 12, 345_678)));

    // Live 2026-11-13, a level 70 barbarian at paragon 267: the experience to the next level kept 7240.
    [Fact]
    public void At_the_cap_a_leftover_to_the_next_level_is_not_said() =>
        Assert.Equal("Level 70, paragon 267. 302040630 experience to the next paragon level.",
            Text(new PlayerExperience(70, 7240, 267, 302_040_630, LevelCap: 70)));

    [Fact]
    public void Below_the_cap_the_level_leads_whatever_the_paragon_reads() =>
        Assert.Equal("Level 20. 6546 experience to the next level.", Text(new PlayerExperience(20, 6546, 0, 0, LevelCap: 70)));

    [Fact]
    public void With_nothing_left_to_earn_it_says_the_level() => Assert.Equal("Level 60.", Text(new PlayerExperience(60, 0, 300, 0)));

    [Fact]
    public void Without_a_level_it_says_it_cannot_read()
    {
        Assert.Equal(ExperienceStrings.English.Unreadable, Text(null));
        Assert.Equal(ExperienceStrings.English.Unreadable, Text(new PlayerExperience(0, 100, 0, 0)));
    }

    [Fact]
    public void Every_language_fills_in_each_number()
    {
        foreach (var language in Enum.GetValues<SpeechLanguage>())
        {
            var strings = ExperienceStrings.For(language);
            string next = ExperienceAnnouncer.Text(new PlayerExperience(37, 4242, 0, 0), strings);
            string paragon = ExperienceAnnouncer.Text(new PlayerExperience(60, 0, 12, 4242), strings);
            Assert.Contains("37", next);
            Assert.Contains("4242", next);
            Assert.Contains("60", paragon);
            Assert.Contains("12", paragon);
            Assert.Contains("4242", paragon);
            Assert.NotEmpty(strings.Unreadable);
        }
    }
}
