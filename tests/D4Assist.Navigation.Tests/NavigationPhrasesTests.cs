using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class NavigationPhrasesTests
{
    [Fact]
    public void Dead_end_has_a_fixed_phrase_regardless_of_openings_passed_in()
    {
        Assert.Equal("Callejón sin salida.", NavigationPhrases.Spanish.Announcement(RoomKind.DeadEnd, new List<string> { "arriba" }));
    }

    [Fact]
    public void Junction_lists_its_openings()
    {
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.Junction, new List<string> { "arriba", "derecha", "abajo a la izquierda" });
        Assert.Equal("Cruce. Salidas: arriba, derecha, abajo a la izquierda.", text);
    }

    [Fact]
    public void Big_room_uses_its_own_label()
    {
        // Singular header: exactly one spoken opening -> "Salida:", not "Salidas:".
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.BigRoom, new List<string> { "arriba" });
        Assert.Equal("Sala grande. Salida: arriba.", text);
    }

    [Fact]
    public void Unexplored_direction_appends_the_qualifier()
    {
        Assert.Equal("arriba sin explorar", NavigationPhrases.Spanish.DirectionPhrase(StickDirection.Up, unexplored: true));
        Assert.Equal("arriba", NavigationPhrases.Spanish.DirectionPhrase(StickDirection.Up, unexplored: false));
    }

    [Fact]
    public void Repeated_phrases_are_grouped_with_a_count_word_keeping_first_occurrence_order()
    {
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.Junction,
            new List<string> { "arriba", "abajo a la derecha", "abajo a la derecha" });
        Assert.Equal("Cruce. Salidas: arriba, dos salidas abajo a la derecha.", text);
    }

    [Fact]
    public void Three_identical_phrases_group_into_a_single_count_word_entry()
    {
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.Junction,
            new List<string> { "abajo a la derecha", "abajo a la derecha", "abajo a la derecha" });
        Assert.Equal("Cruce. Salidas: tres salidas abajo a la derecha.", text);
    }

    [Fact]
    public void A_single_opening_uses_the_singular_header()
    {
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.Junction, new List<string> { "arriba" });
        Assert.Equal("Cruce. Salida: arriba.", text);
    }

    [Fact]
    public void Zero_openings_on_a_non_dead_end_room_says_no_other_exits()
    {
        Assert.Equal("Sala grande. Sin otras salidas.", NavigationPhrases.Spanish.Announcement(RoomKind.BigRoom, new List<string>()));
    }

    [Fact]
    public void Entrance_flag_overrides_the_label_regardless_of_kind()
    {
        var text = NavigationPhrases.Spanish.Announcement(RoomKind.DeadEnd, new List<string> { "arriba" }, isEntrance: true);
        Assert.Equal("Entrada. Salida: arriba.", text);
    }
}

public class EnglishPhrasesTests
{
    private static readonly NavigationPhrases En = NavigationPhrases.English;

    [Fact]
    public void Directions_follow_the_clockwise_stick_order()
    {
        Assert.Equal(
            ["up", "up-right", "right", "down-right", "down", "down-left", "left", "up-left"],
            Enum.GetValues<StickDirection>().Select(En.Direction));
    }

    [Fact]
    public void Single_and_plural_headers()
    {
        Assert.Equal("Junction. Exit: up.", En.Announcement(RoomKind.Junction, ["up"]));
        Assert.Equal("Junction. Exits: up, right.", En.Announcement(RoomKind.Junction, ["up", "right"]));
    }

    [Fact]
    public void Grouped_unexplored_example_from_the_spec()
    {
        var phrases = new List<string> { "left", "left", En.DirectionPhrase(StickDirection.UpLeft, unexplored: true) };
        Assert.Equal("Big room. Exits: two exits left, up-left unexplored.", En.Announcement(RoomKind.BigRoom, phrases));
    }

    [Fact]
    public void Count_words_two_to_four_then_digits()
    {
        Assert.Contains("three exits up", En.Announcement(RoomKind.Junction, ["up", "up", "up"]));
        Assert.Contains("four exits up", En.Announcement(RoomKind.Junction, ["up", "up", "up", "up"]));
        Assert.Contains("5 exits up", En.Announcement(RoomKind.Junction, ["up", "up", "up", "up", "up"]));
    }

    [Fact]
    public void Fixed_labels()
    {
        Assert.Equal("Dead end.", En.Announcement(RoomKind.DeadEnd, ["up"]));
        Assert.Equal("Corridor. No other exits.", En.Announcement(RoomKind.Corridor, []));
        Assert.Equal("Entrance. Exit: up.", En.Announcement(RoomKind.DeadEnd, ["up"], isEntrance: true));
        Assert.Equal("All explored.", En.AllExplored);
        Assert.Equal("Exit not found. Rechecking dead ends.", En.Rechecking);
        Assert.Equal("Salida no encontrada. Revisando los callejones.", NavigationPhrases.Spanish.Rechecking);
    }

    [Fact]
    public void Language_setting_switches_live_and_raises_only_on_change()
    {
        var setting = new LanguageSetting();
        var seen = new List<SpeechLanguage>();
        setting.Changed += seen.Add;

        setting.Set(SpeechLanguage.Spanish);
        setting.Set(SpeechLanguage.English);
        setting.Set(SpeechLanguage.English);

        Assert.Equal([SpeechLanguage.English], seen);
        Assert.Same(NavigationPhrases.English, NavigationPhrases.For(setting.Current));
    }
}

public class CompassDirectionTests
{
    public static TheoryData<NavigationPhrases, string[]> Compass => new()
    {
        { NavigationPhrases.English, ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"] },
        { NavigationPhrases.Spanish, ["norte", "noreste", "este", "sureste", "sur", "suroeste", "oeste", "noroeste"] },
    };

    [Theory, MemberData(nameof(Compass))]
    public void Compass_style_names_the_eight_screen_sectors_clockwise_from_up(NavigationPhrases phrases, string[] expected) =>
        Assert.Equal(expected, Enum.GetValues<StickDirection>().Select(d => phrases.Direction(d, DirectionStyle.Compass)));

    [Fact]
    public void Default_style_is_unchanged()
    {
        Assert.Equal("up-right", NavigationPhrases.English.Direction(StickDirection.UpRight));
        Assert.Equal("arriba a la derecha", NavigationPhrases.Spanish.Direction(StickDirection.UpRight, DirectionStyle.Screen));
    }

    [Fact]
    public void Compass_words_fit_the_sentence_formats()
    {
        var en = NavigationPhrases.English;
        var phrases = new List<string> { en.DirectionPhrase(StickDirection.UpRight, false, DirectionStyle.Compass), en.DirectionPhrase(StickDirection.Left, true, DirectionStyle.Compass) };
        Assert.Equal("Junction. Exits: northeast, west unexplored.", en.Announcement(RoomKind.Junction, phrases));
    }

    [Fact]
    public void Every_language_has_distinct_compass_words()
    {
        foreach (var language in SpeechLanguages.Supported)
        {
            var words = Enum.GetValues<StickDirection>().Select(d => NavigationPhrases.For(language).Direction(d, DirectionStyle.Compass)).ToList();
            Assert.All(words, w => Assert.False(string.IsNullOrWhiteSpace(w)));
            Assert.Equal(8, words.Distinct().Count());
        }
    }
}
