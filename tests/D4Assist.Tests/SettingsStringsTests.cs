using D4Assist.Config;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class SettingsStringsTests
{
    public static TheoryData<SettingsStrings> Languages => new() { SettingsStrings.Spanish, SettingsStrings.English };

    [Theory, MemberData(nameof(Languages))]
    public void Every_group_and_cue_has_a_name(SettingsStrings strings)
    {
        Assert.All(Enum.GetValues<SoundGroup>(), group => Assert.False(string.IsNullOrWhiteSpace(strings.Groups.GetValueOrDefault(group))));
        Assert.All(Enum.GetValues<SoundCue>(), cue => Assert.False(string.IsNullOrWhiteSpace(strings.Cues.GetValueOrDefault(cue))));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Combo_choices_match_the_index_based_combos(SettingsStrings strings)
    {
        Assert.Equal(3, strings.LanguageChoices.Count); // auto, Spanish, English
        Assert.Equal(2, strings.RadarFrameChoices.Count); // cardinal, diagonal
    }
}
