using D4Assist.Config;
using D4Assist.Navigation;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class SettingsStringsTests
{
    public static TheoryData<SpeechLanguage> Languages => [.. SpeechLanguages.Supported];

    [Theory, MemberData(nameof(Languages))]
    public void Every_group_and_cue_has_a_name(SpeechLanguage language)
    {
        var strings = SettingsStrings.For(language);
        Assert.All(Enum.GetValues<SoundGroup>(), group => Assert.False(string.IsNullOrWhiteSpace(strings.Groups.GetValueOrDefault(group))));
        Assert.All(Enum.GetValues<SoundCue>(), cue => Assert.False(string.IsNullOrWhiteSpace(strings.Cues.GetValueOrDefault(cue))));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Combo_choices_match_the_index_based_combos(SpeechLanguage language)
    {
        var strings = SettingsStrings.For(language);
        Assert.False(string.IsNullOrWhiteSpace(strings.LanguageAuto)); // then every language by its own name
        Assert.Equal(2, strings.RadarFrameChoices.Count); // cardinal, diagonal
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_spoken_message_exists(SpeechLanguage language)
    {
        var app = AppStrings.For(language);
        Assert.All(Enum.GetValues<Game.ReadCheck>(), check => Assert.False(string.IsNullOrWhiteSpace(app.ReadFailures.GetValueOrDefault(check))));
        Assert.Equal(8, app.SoundExamples.Count);
        Assert.Contains("{0}", app.ReadWarningFormat);
        Assert.Contains("{0}", app.EnteringWorldFormat);
        Assert.Contains("{1}", app.ShelterReachedFormat);
        var phrases = NavigationPhrases.For(language);
        Assert.All(Enum.GetValues<StickDirection>(), d => Assert.False(string.IsNullOrWhiteSpace(phrases.Direction(d))));
        Assert.False(string.IsNullOrWhiteSpace(phrases.Announcement(RoomKind.Junction, [phrases.DirectionPhrase(StickDirection.Up, true)])));
    }

    [Fact]
    public void Every_language_has_a_code_and_a_name_and_follows_its_game_locale()
    {
        Assert.Equal(13, SpeechLanguages.Supported.Count);
        Assert.All(SpeechLanguages.Supported, l => Assert.Equal(l, SpeechLanguages.FromCode(SpeechLanguages.Code(l))));
        Assert.Equal(SpeechLanguage.French, SpeechLanguages.FromLocale("frFR"));
        Assert.Equal(SpeechLanguage.Portuguese, SpeechLanguages.FromLocale("ptBR"));
        Assert.Equal(SpeechLanguage.ChineseTraditional, SpeechLanguages.FromLocale("zhTW"));
        Assert.Equal(SpeechLanguage.Spanish, SpeechLanguages.FromLocale("esMX"));
        Assert.Equal(SpeechLanguage.English, SpeechLanguages.FromLocale("xxXX"));
    }
}
