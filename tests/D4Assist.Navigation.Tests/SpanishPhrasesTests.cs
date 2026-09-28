using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class SpanishPhrasesTests
{
    [Fact]
    public void Dead_end_has_a_fixed_phrase_regardless_of_openings_passed_in()
    {
        Assert.Equal("Callejón sin salida.", SpanishPhrases.Announcement(RoomKind.DeadEnd, new List<string> { "arriba" }));
    }

    [Fact]
    public void Junction_lists_its_openings()
    {
        var text = SpanishPhrases.Announcement(RoomKind.Junction, new List<string> { "arriba", "derecha", "abajo a la izquierda" });
        Assert.Equal("Cruce. Salidas: arriba, derecha, abajo a la izquierda.", text);
    }

    [Fact]
    public void Big_room_uses_its_own_label()
    {
        // Singular header: exactly one spoken opening -> "Salida:", not "Salidas:".
        var text = SpanishPhrases.Announcement(RoomKind.BigRoom, new List<string> { "arriba" });
        Assert.Equal("Sala grande. Salida: arriba.", text);
    }

    [Fact]
    public void Unexplored_direction_appends_the_qualifier()
    {
        Assert.Equal("arriba sin explorar", SpanishPhrases.DirectionPhrase(StickDirection.Up, unexplored: true));
        Assert.Equal("arriba", SpanishPhrases.DirectionPhrase(StickDirection.Up, unexplored: false));
    }

    [Fact]
    public void Repeated_phrases_are_grouped_with_a_count_word_keeping_first_occurrence_order()
    {
        var text = SpanishPhrases.Announcement(RoomKind.Junction,
            new List<string> { "arriba", "abajo a la derecha", "abajo a la derecha" });
        Assert.Equal("Cruce. Salidas: arriba, dos salidas abajo a la derecha.", text);
    }

    [Fact]
    public void Three_identical_phrases_group_into_a_single_count_word_entry()
    {
        var text = SpanishPhrases.Announcement(RoomKind.Junction,
            new List<string> { "abajo a la derecha", "abajo a la derecha", "abajo a la derecha" });
        Assert.Equal("Cruce. Salidas: tres salidas abajo a la derecha.", text);
    }

    [Fact]
    public void A_single_opening_uses_the_singular_header()
    {
        var text = SpanishPhrases.Announcement(RoomKind.Junction, new List<string> { "arriba" });
        Assert.Equal("Cruce. Salida: arriba.", text);
    }

    [Fact]
    public void Zero_openings_on_a_non_dead_end_room_says_no_other_exits()
    {
        Assert.Equal("Sala grande. Sin otras salidas.", SpanishPhrases.Announcement(RoomKind.BigRoom, new List<string>()));
    }

    [Fact]
    public void Entrance_flag_overrides_the_label_regardless_of_kind()
    {
        var text = SpanishPhrases.Announcement(RoomKind.DeadEnd, new List<string> { "arriba" }, isEntrance: true);
        Assert.Equal("Entrada. Salida: arriba.", text);
    }
}
