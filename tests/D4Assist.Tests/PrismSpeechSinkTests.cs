using D4Assist.Speech;
using Prismatoid;
using Xunit;

namespace D4Assist.Tests;

public class PrismSpeechSinkTests
{
    [Fact]
    public void Under_Wine_only_the_bridged_backends_are_tried_highest_priority_first()
    {
        // PRISM v0.18.3's registry as seen on Windows: SAPI ties with Speech Dispatcher at 97, ahead in the registry.
        var registry = new[]
        {
            new PrismBackendInfo(0x89CC19C5C4AC1A56, "NVDA", 103, true),
            new PrismBackendInfo(0x1D6DF72422CEEE66, "SAPI", 97, true),
            new PrismBackendInfo(0x478B44F14AD3D89C, "Spiel", 96, true),
            new PrismBackendInfo(0xE3D6F895D949EBFE, "Speech Dispatcher", 97, true),
            new PrismBackendInfo(0x10AA1FC05A17F96C, "Orca", 100, true),
        };

        var tried = PrismSpeechSink.WineBridgesByPriority(registry).Select(b => b.Name);

        Assert.Equal(["Orca", "Speech Dispatcher", "Spiel"], tried);
    }
}
