using D4Data.Model;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class CandidateIndexTests
{
    private static CandidateScene Scene(int sno) => new(sno, $"scene{sno}", [32, 32], 0, default!);

    [Fact]
    public void Knows_its_scenes_and_nothing_else()
    {
        var index = new CandidateIndex(new[] { 63, 64, 2_660_000 }.ToDictionary(sno => sno, Scene));

        Assert.Equal(3, index.Count);
        foreach (int sno in new[] { 63, 64, 2_660_000 })
        {
            Assert.True(index.TryGet(sno, out var scene));
            Assert.Equal(sno, scene.Sno);
        }
        foreach (int sno in new[] { 0, 62, 65, 2_660_001, int.MaxValue, -1, int.MinValue }) Assert.False(index.TryGet(sno, out _));
    }

    [Fact]
    public void An_empty_index_knows_nothing()
    {
        Assert.False(new CandidateIndex(new Dictionary<int, CandidateScene>()).TryGet(0, out _));
    }
}
