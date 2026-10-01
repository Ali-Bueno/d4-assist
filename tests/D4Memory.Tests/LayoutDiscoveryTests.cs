using System.Buffers.Binary;
using System.Numerics;
using D4Memory.SceneRoute;

namespace D4Memory.Tests;

public class LayoutDiscoveryTests
{
    private const int PlayerSno = 486910, MonsterSno = 197258, ChestSno = 625841;
    private const long Attributes = 0x1234_5678_0000;
    private static readonly Vector3 Player = new(48.5f, -95.3f, 0.2f);
    private static readonly HashSet<int> ActorSnos = [PlayerSno, MonsterSno, ChestSno];

    // Moved by a pretend update: every field 0x10 (or 8 for the pointer) further into its slot.
    private static readonly RActorFields MovedActor = new(AcdId: 0x1C, PreviousState: 0x148, ActorSno: 0x5C8, Position: 0x770);
    private static readonly AcdFields MovedAcd = new(RActorId: 0x24, ActorSno: 0x60, Position: 0x8C, Attributes: 0x2A8);

    private static int NamedAttributes(long container) => container == Attributes ? 300 : 0;

    // The player and four other actors, each with its ACD; the fields where the layout says.
    private static (List<SlotBytes> RActors, List<SlotBytes> Acds) World(RActorFields actor, AcdFields acd)
    {
        var ractors = new List<SlotBytes>();
        var acds = new List<SlotBytes>();
        var things = new (int Sno, Vector3 At)[]
        {
            (PlayerSno, Player), (MonsterSno, new(52, -90, 0)), (MonsterSno, new(40, -99, 0)), (ChestSno, new(49.7f, -72.3f, 0)),
            (MonsterSno, new(60, -110, 1)),
        };
        for (int i = 0; i < things.Length; i++)
        {
            uint rId = 0x2700016u + (uint)i, aId = 0x1C00035u + (uint)i * 3;
            var r = new byte[0xC30];
            PutU32(r, 0x08, rId);
            PutU32(r, actor.AcdId, aId);
            PutU32(r, actor.ActorSno, (uint)things[i].Sno);
            PutV3(r, actor.Position, things[i].At);
            PutF32(r, actor.Rotation + 12, 1); // unit quaternion (0,0,0,1)
            ractors.Add(new SlotBytes(rId, r));

            var a = new byte[0x6A0];
            PutU32(a, 0x08, aId);
            PutU32(a, acd.RActorId, rId);
            PutU32(a, acd.ActorSno, (uint)things[i].Sno);
            PutV3(a, acd.Position, things[i].At);
            BinaryPrimitives.WriteInt64LittleEndian(a.AsSpan(acd.Attributes), Attributes);
            acds.Add(new SlotBytes(aId, a));
        }
        return (ractors, acds);
    }

    private static LayoutDiscovery.ActorLayout Check(List<SlotBytes> ractors, List<SlotBytes> acds) =>
        LayoutDiscovery.CheckActors(ractors, acds, Player, PlayerSno, ActorSnos.Contains, NamedAttributes, RActorFields.Known, AcdFields.Known);

    [Fact]
    public void TodaysLayout_IsKeptAsKnown()
    {
        var (ractors, acds) = World(RActorFields.Known, AcdFields.Known);

        var layout = Check(ractors, acds);

        Assert.True(layout.Conclusive);
        Assert.Equal(RActorFields.Known, layout.Actor);
        Assert.Equal(AcdFields.Known, layout.Acd);
        Assert.DoesNotContain("MOVED", layout.Report);
    }

    [Fact]
    public void FieldsAnUpdateMoved_AreFoundAgain()
    {
        var (ractors, acds) = World(MovedActor, MovedAcd);

        var layout = Check(ractors, acds);

        Assert.True(layout.Conclusive);
        Assert.Equal(MovedActor, layout.Actor);
        Assert.Equal(MovedAcd, layout.Acd);
        Assert.Contains("RActor position MOVED 0x760 -> 0x770", layout.Report);
    }

    [Fact]
    public void WithoutThePlayersActor_TheLayoutIsKept_AndNotConclusive()
    {
        var (ractors, acds) = World(MovedActor, MovedAcd);

        var layout = LayoutDiscovery.CheckActors(
            ractors.Skip(1).ToList(), acds, Player, PlayerSno, ActorSnos.Contains, NamedAttributes, RActorFields.Known, AcdFields.Known);

        Assert.False(layout.Conclusive);
        Assert.Equal(RActorFields.Known, layout.Actor);
    }

    [Fact]
    public void TheSceneRecord_StaysWhereItMatches_OrIsFoundWhereMostSlotsMatch()
    {
        static byte[] SlotWithRecordAt(int offset) { var s = new byte[0x930]; s[offset] = 0xAB; return s; }
        static bool Matches(byte[] slot, int a) => slot[a] == 0xAB;

        Assert.Equal(0x168, LayoutDiscovery.CheckSceneRecord([SlotWithRecordAt(0x168)], 0x168, Matches));
        Assert.Equal(0x178, LayoutDiscovery.CheckSceneRecord([SlotWithRecordAt(0x178), SlotWithRecordAt(0x178)], 0x168, Matches));
        Assert.Null(LayoutDiscovery.CheckSceneRecord([new byte[0x930]], 0x168, Matches));
    }

    private static void PutU32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static void PutF32(byte[] b, int o, float v) => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o), v);
    private static void PutV3(byte[] b, int o, Vector3 v) { PutF32(b, o, v.X); PutF32(b, o + 4, v.Y); PutF32(b, o + 8, v.Z); }
}
