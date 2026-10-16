using D4Assist.Game;

namespace D4Assist.Tests;

public class QuestOwnerLockTests
{
    // Live 2026-10-16, the Light's Watch: QST_Frac_PaleKnightsKin_Lectern_02 (a quest chest, flags=none) looped while the player had
    // no Pale Knight's Kin quest.
    private const int LecternSno = 703461, PaleKnightsKin = 700000, OtherSno = 1;

    private static int? OwnerOf(int sno) => sno == LecternSno ? PaleKnightsKin : null;

    [Fact]
    public void An_object_of_a_quest_not_followed_is_locked()
    {
        var actors = ActorFilter.LockOthersQuests([new(1, LecternSno, 0, 0, 0), new(2, OtherSno, 0, 0, 0)], OwnerOf, followed: [1931517]);

        Assert.True(actors[0].Unusable);
        Assert.False(actors[1].Unusable);
    }

    [Fact]
    public void It_is_usable_while_its_quest_is_followed_or_names_it()
    {
        Assert.False(ActorFilter.LockOthersQuests([new(1, LecternSno, 0, 0, 0)], OwnerOf, followed: [PaleKnightsKin])[0].Unusable);
        Assert.False(ActorFilter.LockOthersQuests([new(1, LecternSno, 0, 0, 0, QuestStep: true)], OwnerOf, followed: [])[0].Unusable);
    }
}
