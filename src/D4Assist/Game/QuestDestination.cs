using System.Numerics;

namespace D4Assist.Game;

/// <summary>An open-world quest place the assistant leads to over a computed path (QuestPathBridge), where the game's own
/// navigation doesn't: the marker of <paramref name="TargetActor"/> that phase <paramref name="PhaseUid"/> of
/// <paramref name="Quest"/> names. Every name is a CoreTOC name.</summary>
public sealed record QuestDestination(string Name, string WorldName, string Quest, uint PhaseUid, string TargetActor);

/// <summary>A destination resolved offline: the quest and target actor SNOs and the target's world X/Y.</summary>
public sealed record LoadedQuestDestination(QuestDestination Destination, int QuestSno, int TargetSno, Vector2 Target);

/// <summary>The quest places the assistant leads to in the open world, each one a user-approved exception to "the game's
/// navigation covers the open world" (research/campaign-routes.md).</summary>
public static class QuestDestinationCatalog
{
    /// <summary>"Hexenmeister: Jünger des Verbotenen" (QST_Warlock_ClassQ_01, 2449612), first step: walk to a trigger sphere in
    /// the Dry Steppes, about 220 units south of the Farobru waypoint, where the game's navigation assist doesn't lead (user,
    /// 2026-10-19). Its dungeon (QST_Class_Warlock_DGN_01) and the rival near Ked Bardu come after.</summary>
    public static readonly QuestDestination WarlockDisciplesStart = new(
        "warlock-disciples-start", "Sanctuary_Eastern_Continent", "QST_Warlock_ClassQ_01", 0, "Trigger_Sphere_8m");

    public static readonly IReadOnlyList<QuestDestination> All = [WarlockDisciplesStart];
}
