using D4Assist.Strings;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Navigation;

namespace D4Assist.Tests;

public class GameLocaleTests
{
    private const string Line = "[Game] Current Speech Locale: enUS  Current Text Locale: ";

    [Theory]
    [InlineData("enUS", SpeechLanguage.English)]
    [InlineData("esMX", SpeechLanguage.Spanish)]
    [InlineData("esES", SpeechLanguage.Spanish)]
    [InlineData("frFR", SpeechLanguage.English)]
    public void Text_locale_is_parsed_and_mapped(string locale, SpeechLanguage expected)
    {
        var parsed = GameLocale.ExtractTextLocale($"{Line}{locale}\r");
        Assert.Equal(locale, parsed);
        Assert.Equal(expected, GameLocale.ToLanguage(parsed!));
    }

    [Fact]
    public void Lines_without_the_marker_give_null()
    {
        Assert.Null(GameLocale.ExtractTextLocale("[Game] Client entered world | world: X"));
        Assert.Null(GameLocale.ExtractTextLocale(Line));
    }

    [Fact]
    public void Startup_read_takes_the_last_locale_line_and_tail_raises_new_ones()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, WorldLogWatcher.LogFileName);
        File.WriteAllText(path, $"{Line}esMX\n{Line}enUS\n");

        using var watcher = new WorldLogWatcher(dir);
        var seen = new List<string>();
        watcher.TextLocaleChanged += seen.Add;
        File.AppendAllText(path, $"{Line}esES\n");
        watcher.PollOnce();

        Assert.Equal(["esES"], seen);
    }

    [Fact]
    public void Missing_log_gives_no_locale()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var watcher = new WorldLogWatcher(dir);
        Assert.Null(watcher.ReadLastTextLocaleAtStartup());
    }

    [Fact]
    public void Follower_switches_live_when_auto()
    {
        var setting = new LanguageSetting();
        var follower = new LanguageFollower(setting, forced: null);
        follower.OnTextLocale("enUS");
        Assert.Equal(SpeechLanguage.English, setting.Current);
        follower.OnTextLocale("esMX");
        Assert.Equal(SpeechLanguage.Spanish, setting.Current);
    }

    [Fact]
    public void Forced_language_ignores_the_game_locale()
    {
        var setting = new LanguageSetting();
        var follower = new LanguageFollower(setting, SpeechLanguage.English);
        follower.OnTextLocale("esMX");
        Assert.Equal(SpeechLanguage.English, setting.Current);
    }

    [Theory]
    [InlineData("auto", null)]
    [InlineData("es", SpeechLanguage.Spanish)]
    [InlineData("EN", SpeechLanguage.English)]
    public void Config_language_values(string value, SpeechLanguage? expected) =>
        Assert.Equal(expected, new AppConfig { Language = value }.ForcedLanguage());

    [Fact]
    public void Config_invalid_language_is_a_hard_error()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"Language\":\"fr\"}");
        Assert.Throws<InvalidDataException>(() => AppConfig.Load(path));
        Assert.Equal("auto", new AppConfig().Language);
    }

    [Fact]
    public void App_strings_are_language_aware()
    {
        Assert.Equal("Dungeon assistant active", AppStrings.For(SpeechLanguage.English).AssistantActive);
        Assert.Equal("Asistente de mazmorras activo", AppStrings.For(SpeechLanguage.Spanish).AssistantActive);
        Assert.Equal("Entering X", AppStrings.English.EnteredWorld("X"));
        Assert.Equal("Guide off.", AppStrings.English.GuideOff);
    }
}
