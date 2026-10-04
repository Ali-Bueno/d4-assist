using System.Numerics;
using D4Assist.Game;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class SwitchStateLogTests
{
    // QST_Naha_TombOfAkarat_EruTrial_Brazier, a switch lit again and again (research/dungeon-objectives.md §17).
    private const int BrazierSno = 1957286, ChestSno = 2;

    private static RActorRecord Record(uint id, int sno, uint previousState = 0, int placement = RActorReader.PlacedValue) =>
        new(SlotAddress: 0, id, AcdId: id, sno, new Vector3(12.34f, -5f, 0), Quaternion.Identity, WorldId: 0, SceneId: 0, placement, previousState);

    [Fact]
    public void Logs_a_switch_when_seen_and_on_each_change_only()
    {
        var lines = new List<string>();
        var log = new SwitchStateLog(sno => sno == BrazierSno, _ => "Brazier", lines.Add);
        var attributes = new Dictionary<uint, IReadOnlyDictionary<string, uint>>();
        string[] known = [AcdFlagReader.GizmoState, AcdFlagReader.GizmoOperated];
        void Poll(params RActorRecord[] records) => log.Observe(records, r => attributes.GetValueOrDefault(r.Id), known);

        Poll(Record(1, BrazierSno), Record(2, ChestSno), Record(3, BrazierSno, placement: -1));
        Poll(Record(1, BrazierSno));
        attributes[1] = new Dictionary<string, uint> { [AcdFlagReader.GizmoState] = 1 };
        Poll(Record(1, BrazierSno, previousState: 0x61));

        Assert.Equal(
        [
            "SWITCH Brazier sno=1957286 at (12.3,-5.0): seen PreviousState=0 Gizmo_State=0 Gizmo_Has_Been_Operated=0",
            "SWITCH Brazier sno=1957286 at (12.3,-5.0): PreviousState=0→97, Gizmo_State=0→1",
        ], lines);
    }
}
