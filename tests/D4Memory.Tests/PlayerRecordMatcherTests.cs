using System.Buffers.Binary;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class PlayerRecordMatcherTests
{
    private const int WorldSno = 708553;
    private const int ActorSno = 176832; // barbarianM, per research/client-memory.md §3
    private const int SceneSno = 999001;

    private static readonly HashSet<int> ActorSnos = [ActorSno];
    private static readonly HashSet<int> SceneSnos = [SceneSno];

    /// <summary>Builds a byte buffer holding one valid player record with `p` (the position) at <paramref name="p"/>.</summary>
    private static byte[] MakeRecord(int bufferSize, int p, int worldSno = WorldSno, int actorSno = ActorSno,
        int sceneSno = SceneSno, uint zeroMarker = 0, (float X, float Y, float Z)? position = null,
        uint leadingWord = 0xCDCD0001, uint trailingWord = 1)
    {
        var buffer = new byte[bufferSize];
        var pos = position ?? (100f, 200f, 3.5f);
        WriteU32(buffer, p - 20, leadingWord);
        WriteI32(buffer, p - 16, sceneSno);
        WriteI32(buffer, p - 12, worldSno);
        WriteI32(buffer, p - 8, actorSno);
        WriteU32(buffer, p - 4, zeroMarker);
        WriteVec3(buffer, p, pos);
        WriteU32(buffer, p + 12, trailingWord);
        return buffer;
    }

    [Fact]
    public void MatchesAValidRecordAndReportsItsFields()
    {
        var buffer = MakeRecord(bufferSize: 200, p: 100);

        var hits = PlayerRecordMatcher.FindHits(buffer, baseAddress: 0x3000, ActorSnos, WorldSno, SceneSnos);

        var hit = Assert.Single(hits);
        Assert.Equal(0x3000 + 100, hit.PositionAddress);
        Assert.Equal(SceneSno, hit.SceneSno);
        Assert.Equal(WorldSno, hit.WorldSno);
        Assert.Equal(ActorSno, hit.ActorSno);
        Assert.Equal(100f, hit.Position.X, 3);
        Assert.Equal(200f, hit.Position.Y, 3);
        Assert.Equal(0xCDCD0001u, hit.LeadingWord);
        Assert.Equal(1u, hit.TrailingWord);
    }

    [Fact]
    public void RejectsNonZeroZeroMarker()
    {
        var buffer = MakeRecord(200, 100, zeroMarker: 1);

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void RejectsUnknownActorSno()
    {
        var buffer = MakeRecord(200, 100, actorSno: 999);

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void RejectsWrongWorldSno()
    {
        var buffer = MakeRecord(200, 100, worldSno: WorldSno + 1);

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void RejectsUnknownSceneSno()
    {
        var buffer = MakeRecord(200, 100, sceneSno: 42);

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void RejectsNonFinitePosition()
    {
        var buffer = MakeRecord(200, 100, position: (float.NaN, 0f, 0f));

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void RejectsPositionOutsideTheSaneBound()
    {
        var tooFar = PlayerRecordMatcher.PositionBoundUnits * 2f;
        var buffer = MakeRecord(200, 100, position: (tooFar, 0f, 0f));

        Assert.Empty(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void DoesNotRequireTheUnconfirmedLeadingOrTrailingWords()
    {
        // Task spec: p-20 and p+12 are reported, never required for a match.
        var buffer = MakeRecord(200, 100, leadingWord: 0, trailingWord: 0);

        Assert.Single(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void SkipsRecordsThatWouldReadPastEitherWindowEdgeWithoutCrashing()
    {
        var nearStart = new byte[200];
        WriteI32(nearStart, 16 - 12, WorldSno); // p=16: p-20 < 0, not enough bytes before `p` for RecordBytesBefore

        var nearEnd = new byte[200];
        WriteI32(nearEnd, 190 - 12, WorldSno); // p=190: p+16 > 200, not enough bytes after `p` for RecordBytesAfter

        Assert.Empty(PlayerRecordMatcher.FindHits(nearStart, 0, ActorSnos, WorldSno, SceneSnos));
        Assert.Empty(PlayerRecordMatcher.FindHits(nearEnd, 0, ActorSnos, WorldSno, SceneSnos));
    }

    [Fact]
    public void MatchesExactlyAtTheWindowEdge()
    {
        int p = PlayerRecordMatcher.RecordBytesBefore;
        int size = p + PlayerRecordMatcher.RecordBytesAfter;
        var buffer = MakeRecord(size, p);

        Assert.Single(PlayerRecordMatcher.FindHits(buffer, 0, ActorSnos, WorldSno, SceneSnos));
    }

    private static void WriteI32(byte[] b, int off, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(off), v);
    private static void WriteU32(byte[] b, int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off), v);

    private static void WriteVec3(byte[] b, int off, (float X, float Y, float Z) v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off), v.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 4), v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 8), v.Z);
    }
}
