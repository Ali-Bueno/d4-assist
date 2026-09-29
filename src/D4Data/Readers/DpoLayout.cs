using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>Offsets and strides needed to walk zone_progress and a DPO quest, all read by name from definitions.json.</summary>
public sealed record DpoLayout(
    int GlobalContentField, int ZoneProgressSize, int WorldQuestListField, int WorldQuestStride, int WorldQuestWorldField, int WorldQuestQuestsField,
    int QuestPhasesField, int PhaseStride, int PhaseCallbackSetsField, int CallbackSetStride, int SetCallbacksField, int CallbackStride,
    int CallbackTokensField, int SnoNameStride)
{
    // Size of a quest id in WorldToDPOQuestList.arQuests: a DT_SNO, i32 (walked manually, research/dungeon-objectives.md §1).
    public const int QuestIdSize = 4;

    /// <exception cref="InvalidDataException">The schema's array kinds differ from the ones the walk assumes.</exception>
    public static DpoLayout From(TypeSchema s)
    {
        // Global content and quest phases are polymorphic arrays; every other array on the path is a plain one.
        foreach (var (type, field) in new[] { ("GlobalDefinition", "ptContent"), ("QuestDefinition", "arQuestPhases") })
            if (!s.IsPolymorphicArray(type, field)) throw new InvalidDataException($"{type}.{field} is no longer a polymorphic array.");
        return Build(s);
    }

    private static DpoLayout Build(TypeSchema s) => new(
        s.Offset("GlobalDefinition", "ptContent"), s.Size("GlobalZoneProgressData"),
        s.Offset("GlobalZoneProgressData", "arWorldToDPOQuestList"), s.Size("WorldToDPOQuestList"),
        s.Offset("WorldToDPOQuestList", "snoWorld"), s.Offset("WorldToDPOQuestList", "arQuests"),
        s.Offset("QuestDefinition", "arQuestPhases"), s.Size("QuestPhase"),
        s.Offset("QuestPhase", "arCallbackSets"), s.Size("QuestObjectiveSet"),
        s.Offset("QuestObjectiveSet", "arCallbacks"), s.Size("QuestCallback"),
        s.Offset("QuestCallback", "arSnonameTokens"), s.Size("DT_SNO_NAME"));
}
