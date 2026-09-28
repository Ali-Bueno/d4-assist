using System.Buffers.Binary;
using D4Data.Model;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class CSceneRecordMatcherTests
{
    // Offline facts for SNO 1234: a 32x32x2 tile centered at (16,16,0), matching the record built below.
    private static readonly CandidateScene Candidate =
        new(1234, "Test_Scene", [32f, 32f], ExitMask: 15, Bounds: new Box([0f, 0f, -1f], [32f, 32f, 1f]));

    private static readonly Dictionary<int, CandidateScene> Candidates = new() { [1234] = Candidate };

    private static readonly (float X, float Y, float Z) Wp = (64f, 96f, 3.5f);
    private static readonly (float X, float Y, float Z, float W) IdentityQuat = (0f, 0f, 0f, 1f);
    private const uint TileHash = 0x20003; // (64/32)<<16 | (96/32) == the formula's own prediction

    /// <summary>Builds a byte buffer holding one valid CScene record with `sno` at <paramref name="a"/>.</summary>
    private static byte[] MakeRecord(int bufferSize, int a, int sno = 1234, uint tileHash = TileHash,
        int exitMask = 15, uint hasInfoFlag = 1, (float X, float Y, float Z, float W)? quat = null,
        (float X, float Y, float Z)? localCenter = null, (float X, float Y, float Z)? worldCenter = null,
        uint tileHashCopy = TileHash, int? exitMaskCopy = null, uint nodeId = 0, uint groupId = 0)
    {
        var buffer = new byte[bufferSize];
        var q = quat ?? IdentityQuat;
        var lc = localCenter ?? (16f, 16f, 0f);
        var lh = (16f, 16f, 1f);
        var wc = worldCenter ?? (lc.Item1 + Wp.X, lc.Item2 + Wp.Y, lc.Item3 + Wp.Z);
        var wh = lh;

        WriteI32(buffer, a, sno);
        WriteU32(buffer, a - 48, tileHash);
        WriteI32(buffer, a - 44, exitMask);
        WriteU32(buffer, a - 40, nodeId);
        WriteU32(buffer, a - 36, groupId);
        WriteU32(buffer, a - 20, hasInfoFlag);
        WriteU64(buffer, a - 16, 0xDEADBEEF12345678UL);
        WriteQuat(buffer, a + 80, q);
        WriteVec3(buffer, a + 96, Wp);
        WriteVec3(buffer, a + 136, lc);
        WriteVec3(buffer, a + 148, lh);
        WriteVec3(buffer, a + 160, wc);
        WriteVec3(buffer, a + 172, wh);
        WriteU32(buffer, a + 416, tileHashCopy);
        WriteI32(buffer, a + 420, exitMaskCopy ?? exitMask);
        return buffer;
    }

    [Fact]
    public void MatchesAValidRecordAndReportsItsFields()
    {
        var buffer = MakeRecord(bufferSize: 600, a: 100, nodeId: 7, groupId: 3);

        var hits = CSceneRecordMatcher.FindHits(buffer, baseAddress: 0x1000, new CandidateIndex(Candidates));

        var hit = Assert.Single(hits);
        Assert.Equal(0x1000 + 100, hit.Address);
        Assert.Equal(1234, hit.Sno);
        Assert.Equal(15, hit.ExitMask);
        Assert.Equal(TileHash, hit.TileHash);
        Assert.True(hit.HashMatchesFormula);
        Assert.Equal(64f, hit.Wp.X, 3);
        Assert.Equal(96f, hit.Wp.Y, 3);
        Assert.Equal(0xDEADBEEF12345678UL, hit.Vtable);
        Assert.Equal(7u, hit.NodeId);
        Assert.Equal(3u, hit.GroupId);
    }

    [Fact]
    public void ReportsFormulaMismatchWithoutRejectingTheHit()
    {
        // A hash that still matches its own copy (a+416) but disagrees with the (wp/32) formula:
        // the task spec says the formula is only ever reported, never required.
        var buffer = MakeRecord(600, 100, tileHash: 0xABCDEF, tileHashCopy: 0xABCDEF);

        var hits = CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates));

        var hit = Assert.Single(hits);
        Assert.False(hit.HashMatchesFormula);
    }

    [Fact]
    public void RejectsNonUnitQuaternion()
    {
        var buffer = MakeRecord(600, 100, quat: (0f, 0f, 0f, 5f));

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsLocalAabbMismatch()
    {
        var buffer = MakeRecord(600, 100, localCenter: (17f, 16f, 0f)); // off by more than AabbTolerance

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsWorldAabbCenterMismatch()
    {
        var buffer = MakeRecord(600, 100, worldCenter: (999f, 999f, 999f));

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsMissingHasDrlgInfoFlag()
    {
        var buffer = MakeRecord(600, 100, hasInfoFlag: 0);

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void AcceptsLiveExitMaskThatIsASubsetOfTheOfflineOne()
    {
        // A piece of a large preset room uses only some of its exits (Triad_B_ThroneRoom_N02_E03_DGN: offline 15, live 7).
        var buffer = MakeRecord(600, 100, exitMask: 7, exitMaskCopy: 7);

        var hit = Assert.Single(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
        Assert.Equal(7, hit.ExitMask);
    }

    [Fact]
    public void RejectsLiveExitTheOfflineSceneDoesNotHave()
    {
        var northOnly = new Dictionary<int, CandidateScene> { [1234] = Candidate with { ExitMask = 1 } };
        var buffer = MakeRecord(600, 100, exitMask: 3, exitMaskCopy: 3); // S exit the offline scene lacks

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(northOnly)));
    }

    [Fact]
    public void RejectsExitMaskCopyMismatch()
    {
        var buffer = MakeRecord(600, 100, exitMaskCopy: 7); // -44 == offline (15) but +420 disagrees

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsTileHashCopyMismatch()
    {
        var buffer = MakeRecord(600, 100, tileHashCopy: 0xBAD);

        Assert.Empty(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void SkipsRecordsThatWouldReadPastEitherWindowEdgeWithoutCrashing()
    {
        // Only the sno field is written; a full record wouldn't fit before/after it in these small buffers.
        // The matcher must reject on the bounds check before attempting to read any other field.
        var nearStart = new byte[600];
        WriteI32(nearStart, 8, 1234); // not enough bytes before `a` for RecordBytesBefore

        var nearEnd = new byte[200];
        WriteI32(nearEnd, 100, 1234); // not enough bytes after `a` for RecordBytesAfter

        Assert.Empty(CSceneRecordMatcher.FindHits(nearStart, 0, new CandidateIndex(Candidates)));
        Assert.Empty(CSceneRecordMatcher.FindHits(nearEnd, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void MatchesExactlyAtTheWindowEdge()
    {
        // The smallest buffer that still has exactly RecordBytesBefore/After bytes around `a`.
        int a = CSceneRecordMatcher.RecordBytesBefore;
        int size = a + CSceneRecordMatcher.RecordBytesAfter;
        var buffer = MakeRecord(size, a);

        Assert.Single(CSceneRecordMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void TryMatchAtValidatesTheSameFieldsAsFindHitsForAKnownOffset()
    {
        // SceneRoute already knows the offset (from the pool's chunk/stride math); it calls TryMatchAt
        // directly instead of scanning, so this must accept a good record and reject a bad one identically.
        var buffer = MakeRecord(600, a: 100);

        Assert.True(CSceneRecordMatcher.TryMatchAt(buffer, 100, new CandidateIndex(Candidates), out var hit));
        Assert.Equal(1234, hit.Sno);

        var badBuffer = MakeRecord(600, a: 100, hasInfoFlag: 0);
        Assert.False(CSceneRecordMatcher.TryMatchAt(badBuffer, 100, new CandidateIndex(Candidates), out _));
    }

    private static void WriteI32(byte[] b, int off, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(off), v);
    private static void WriteU32(byte[] b, int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off), v);
    private static void WriteU64(byte[] b, int off, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(off), v);

    private static void WriteVec3(byte[] b, int off, (float X, float Y, float Z) v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off), v.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 4), v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 8), v.Z);
    }

    private static void WriteQuat(byte[] b, int off, (float X, float Y, float Z, float W) q)
    {
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off), q.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 4), q.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 8), q.Z);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off + 12), q.W);
    }
}
