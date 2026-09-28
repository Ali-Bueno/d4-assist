using System.Buffers.Binary;
using D4Data.Model;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class RevealedListMatcherTests
{
    private static readonly Dictionary<int, CandidateScene> Candidates = new()
    {
        [1234] = new CandidateScene(1234, "Test_Scene", [32f, 32f], 15, new Box([0f, 0f, 0f], [32f, 32f, 0f])),
    };

    private static byte[] MakeRecord(int bufferSize, int a, uint idSScene = 0x80005, uint marker = 0xA,
        int sno = 1234, uint flag = 1, uint k = 42)
    {
        var buffer = new byte[bufferSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(a - 8), idSScene);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(a - 4), marker);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(a), sno);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(a + 4), flag);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(a + 8), k);
        return buffer;
    }

    [Fact]
    public void MatchesAValidEntry()
    {
        var buffer = MakeRecord(100, a: 48);

        var hits = RevealedListMatcher.FindHits(buffer, baseAddress: 0x2000, new CandidateIndex(Candidates));

        var hit = Assert.Single(hits);
        Assert.Equal(0x2000 + 48, hit.Address);
        Assert.Equal(0x80005u, hit.IdSScene);
        Assert.Equal(1234, hit.Sno);
        Assert.Equal(42u, hit.K);
    }

    [Fact]
    public void RejectsIdSSceneOutOfRange()
    {
        var buffer = MakeRecord(100, 48, idSScene: 0x70000);

        Assert.Empty(RevealedListMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsWrongMarkerConstant()
    {
        var buffer = MakeRecord(100, 48, marker: 0xB);

        Assert.Empty(RevealedListMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void RejectsWrongFlag()
    {
        var buffer = MakeRecord(100, 48, flag: 2);

        Assert.Empty(RevealedListMatcher.FindHits(buffer, 0, new CandidateIndex(Candidates)));
    }

    [Fact]
    public void SkipsRecordsAtTheWindowEdgeWithoutCrashing()
    {
        // `a` == 0 (4-aligned): idSScene would be read at offset -8, before the buffer starts.
        var nearStart = new byte[100];
        BinaryPrimitives.WriteInt32LittleEndian(nearStart.AsSpan(0), 1234);

        Assert.Empty(RevealedListMatcher.FindHits(nearStart, 0, new CandidateIndex(Candidates)));
    }
}
