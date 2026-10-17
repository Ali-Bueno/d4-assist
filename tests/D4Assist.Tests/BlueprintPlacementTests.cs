using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

public class BlueprintPlacementTests
{
    // CSD_Skov_Temis_SmugglersRun_02_BPMS (live 2026-10-17): the explorer's notes and the demonic barrier anchor it at (-128,-128).
    private const int NotesSno = 2578388, BarrierSno = 726309, TriggerSno = 42298;
    private static readonly Vector3 Shift = new(-128, -128, 0);

    private static readonly Blueprint SmugglersRun = new(2241606,
    [
        Marker(NotesSno, 172.2f, 141.0f), Marker(BarrierSno, 15.8f, 38.8f),
        Marker(TriggerSno, 140.6f, 121.6f), Marker(TriggerSno, 26.4f, 26.3f), Marker(TriggerSno, 38.2f, 38.4f),
    ]);

    private static PlacedMarker Marker(int sno, float x, float y) => new(Group: 1, sno, new Vector3(x, y, 0));

    private static (int, Vector3) Live(int sno, float x, float y) => (sno, new Vector3(x, y, 0));

    [Fact]
    public void Two_kinds_of_actor_at_their_markers_place_the_blueprint()
    {
        var shift = BlueprintPlacement.Locate(SmugglersRun.Anchors, [Live(NotesSno, 44.2f, 13.0f), Live(BarrierSno, -112.2f, -89.2f)]);

        Assert.NotNull(shift);
        Assert.InRange(Vector3.Distance(Shift, shift!.Value), 0f, 0.01f);
    }

    [Fact]
    public void One_kind_alone_places_nothing_for_it_could_be_any_of_its_markers()
    {
        // A trigger of the blueprint's three: each shift fits one marker as well as another.
        Assert.Null(BlueprintPlacement.Locate(SmugglersRun.Anchors, [Live(TriggerSno, 12.6f, -6.4f)]));
        Assert.Null(BlueprintPlacement.Locate(SmugglersRun.Anchors, [Live(NotesSno, 44.2f, 13.0f), Live(BarrierSno, 0, 0)]));
    }

    [Fact]
    public void Once_placed_the_named_markers_land_in_the_world_and_stay()
    {
        var placement = new BlueprintPlacement();
        BlueprintMarker[] named = [new(SmugglersRun, Marker(TriggerSno, 26.4f, 26.3f)), new(SmugglersRun, Marker(TriggerSno, 38.2f, 38.4f))];

        Assert.Empty(placement.Place(named, [Live(TriggerSno, 12.6f, -6.4f)]));

        var placed = placement.Place(named, [Live(NotesSno, 44.2f, 13.0f), Live(BarrierSno, -112.2f, -89.2f)]).ToList();
        Assert.Equal([(-101.6f, -101.7f), (-89.8f, -89.6f)], placed.Select(m => (MathF.Round(m.Position.X, 1), MathF.Round(m.Position.Y, 1))));

        Assert.Equal(2, placement.Place(named, []).Count()); // the anchors out of range again
    }
}
