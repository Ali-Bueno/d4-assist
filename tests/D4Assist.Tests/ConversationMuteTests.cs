using D4Assist.Game;
using D4Memory.Ui;

namespace D4Assist.Tests;

public class ConversationMuteTests
{
    // Prologue menu with Vani, live 2026-09-29: an empty option, then four conversations.
    private const int Leave = 0, Village08 = 641998, Opt03 = 843567, Opt06 = 1088736, Opt05 = 976538, Bark = 1234;
    private static readonly int[] Menu = [Leave, Village08, Opt03, Opt06, Opt05];

    private static (ConversationMute Mute, List<bool> Applied) Create()
    {
        var applied = new List<bool>();
        return (new ConversationMute(applied.Add, _ => null), applied);
    }

    private static ConversationSnapshot Snap(int[] menu, params int[] playing) => new(menu, playing);

    [Fact]
    public void MenuThenChosenConversation_StaysMutedUntilItEnds()
    {
        var (mute, applied) = Create();

        mute.OnSnapshot(Snap(Menu));
        mute.OnSnapshot(Snap([], Opt03));
        Assert.True(mute.IsMuted);

        mute.OnSnapshot(Snap([]));
        Assert.False(mute.IsMuted);
        Assert.Equal([true, false], applied);
    }

    [Fact]
    public void ABarkOrTauntNeverMutes()
    {
        var (mute, applied) = Create();

        mute.OnSnapshot(Snap([], Bark));

        Assert.False(mute.IsMuted);
        Assert.Empty(applied);
    }

    [Fact]
    public void AnOldMenuDoesNotMuteLaterConversations()
    {
        var (mute, _) = Create();
        mute.OnSnapshot(Snap(Menu));
        mute.OnSnapshot(Snap([], Opt03));
        mute.OnSnapshot(Snap([]));

        mute.OnSnapshot(Snap([], Opt06)); // same NPC's line started without the menu

        Assert.False(mute.IsMuted);
    }

    [Fact]
    public void Reset_Unmutes()
    {
        var (mute, applied) = Create();
        mute.OnSnapshot(Snap(Menu));

        mute.Reset();

        Assert.False(mute.IsMuted);
        Assert.Equal([true, false], applied);
    }
}
