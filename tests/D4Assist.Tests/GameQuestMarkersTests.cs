using D4Assist.Game;

namespace D4Assist.Tests;

// The Corrosive Swamp's Maggot Queen step (tester log 2026-11-18): the map's quest marker at her hole is the only lead.
public class GameQuestMarkersTests
{
    private const int ProxySno = 56602, LocationSno = 41245, HoleSno = 1338803, CampfireSno = 970455, LevelArea = 600744;

    private static readonly Dictionary<int, string> Names = new()
    {
        [ProxySno] = "Symbol_Quest_Proxy",
        [LocationSno] = "MarkerLocation",
        [HoleSno] = "Maggot_Prop_Hole_Edge_3x3_01_Arrangement_SP",
    };

    private static bool IsGameMarker(MarkedActor m) => GameQuestMarkers.Is(m, sno => Names.GetValueOrDefault(sno));

    private static MarkedActor Marker(int sno, float x, float y, int kind = GameQuestMarkers.Kind) => new(sno, x, y, 0, LevelArea, kind);

    [Fact]
    public void Only_the_generic_quest_marker_actors_of_the_quest_kind_count()
    {
        Assert.True(IsGameMarker(Marker(ProxySno, 0, 0)));
        Assert.True(IsGameMarker(Marker(LocationSno, 0, 0)));
        Assert.False(IsGameMarker(Marker(HoleSno, 0, 0)));
        Assert.False(IsGameMarker(Marker(ProxySno, 0, 0, kind: 11)));
    }

    [Fact]
    public void A_game_marker_no_quest_names_becomes_a_marked_place()
    {
        QuestPlace[] read = [new(CampfireSno, -5.4f, -40.9f)];
        var places = GameQuestMarkers.WithMarkers(read, [Marker(HoleSno, 111.3f, -13.6f), Marker(ProxySno, 111.3f, -13.6f)], IsGameMarker);

        Assert.Equal(2, places.Count);
        var added = places[1];
        Assert.Equal((ProxySno, 111.3f, -13.6f, true), (added.ActorSno, added.X, added.Y, added.Marked));
    }

    [Fact]
    public void A_game_marker_at_a_place_the_quest_names_adds_nothing()
    {
        QuestPlace[] read = [new(LocationSno, -11.5f, -44.3f)];
        var places = GameQuestMarkers.WithMarkers(read, [Marker(LocationSno, -11.4f, -44.0f)], IsGameMarker);

        Assert.Same(read, places);
    }
}
