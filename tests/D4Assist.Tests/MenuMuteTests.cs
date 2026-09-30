using D4Assist.Game;

namespace D4Assist.Tests;

public class MenuMuteTests
{
    private const string Panel = "screen_MenuPanel_Default", Main = "screen_MainInGameMenu";

    private static (MenuMute Mute, List<bool> Applied, List<string> Logs) Create()
    {
        var applied = new List<bool>();
        var logs = new List<string>();
        return (new MenuMute(applied.Add, logs.Add), applied, logs);
    }

    [Fact]
    public void OpenMutesAndCloseUnmutes()
    {
        var (mute, applied, logs) = Create();

        mute.OnScreens([]);
        mute.OnScreens([Main]);
        Assert.True(mute.IsMuted);
        mute.OnScreens([]);

        Assert.False(mute.IsMuted);
        Assert.Equal([true, false], applied);
        Assert.Equal(["MENU closed", $"MENU open: {Main}", "MENU closed"], logs);
    }

    [Fact]
    public void RepeatedStatesAreNeitherAppliedNorLogged()
    {
        var (mute, applied, logs) = Create();

        mute.OnScreens([Panel]);
        mute.OnScreens([Panel]);
        mute.OnScreens([Panel, Main]); // still muted: logged, not re-applied

        Assert.Equal([true], applied);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public void ResetUnmutesAndLetsTheSameStateMuteAgain()
    {
        var (mute, applied, _) = Create();

        mute.OnScreens([Panel]);
        mute.Reset();
        mute.OnScreens([Panel]);

        Assert.Equal([true, false, true], applied);
    }
}
