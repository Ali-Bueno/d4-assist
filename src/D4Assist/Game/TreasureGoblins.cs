using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Treasure goblins: monsters of the family Treasure_Goblin (about 25 variants: treasure_goblin, _gold, _obols, the Undercity's,
/// the seasons'), told by their family rather than their names. They carry no rarity, so the enemy cue never sounded for them;
/// they get the elite's (user request 2026-10-29). The map shows them by their own icon (ActorUIData, <see cref="MinimapInfo"/>).
/// </summary>
public static class TreasureGoblins
{
    public const string Family = "Treasure_Goblin";

    /// <param name="nameOf">An SNO's name (SnoStore.Name), for the monster family.</param>
    public static bool Is(ActorDefinitionInfo info, Func<int, string?> nameOf) =>
        info.EType == Conventions.ActorTypeMonster && info.MonsterFamilySno > 0 && nameOf(info.MonsterFamilySno) == Family;
}
