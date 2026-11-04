using D4Assist.Game;

namespace D4Assist.Tests;

public class CueGateTests
{
    [Theory]
    [InlineData(CueMute.Cutscene, true)]
    [InlineData(CueMute.Cinematic, true)]
    [InlineData(CueMute.Loading, true)]
    [InlineData(CueMute.Menu, false)]
    [InlineData(CueMute.Conversation, false)]
    [InlineData(CueMute.GameInBackground, false)]
    public void Cutscenes_cinematics_and_loading_show_no_gameplay(CueMute reason, bool noGameplay)
    {
        var gate = new CueGate(_ => { });

        gate.Set(reason, true);

        Assert.Equal(noGameplay, gate.ShowsNoGameplay);
        gate.Set(reason, false);
        Assert.False(gate.ShowsNoGameplay);
    }
}
