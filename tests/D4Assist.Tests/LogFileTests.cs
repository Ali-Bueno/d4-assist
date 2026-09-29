using D4Assist.Logging;

namespace D4Assist.Tests;

public class LogFileTests
{
    [Fact]
    public void TimestampWriter_StampsEachLineOnce()
    {
        var inner = new StringWriter { NewLine = "\n" };
        var writer = new TimestampWriter(inner, () => new DateTime(2026, 9, 29, 13, 20, 40, 861)) { NewLine = "\n" };

        writer.Write("World: ");
        writer.WriteLine("DGN_Hawe_HeathensKeep");
        writer.WriteLine("Memory: reading the game now.");

        Assert.Equal("13:20:40.861 World: DGN_Hawe_HeathensKeep\n13:20:40.861 Memory: reading the game now.\n", inner.ToString());
    }

    [Fact]
    public void TeeWriter_WritesToBoth()
    {
        var (first, second) = (new StringWriter(), new StringWriter());

        new TeeWriter(first, second).WriteLine("CUES muted: GameInBackground");

        Assert.Equal(first.ToString(), second.ToString());
        Assert.StartsWith("CUES muted", second.ToString());
    }
}
