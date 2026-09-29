using D4Data.Format;

namespace D4Data.Readers;

/// <summary>Pure walks over the Global zone_progress record and a DPO quest record (research/dungeon-objectives.md §1-2).</summary>
public static class DungeonObjectiveParser
{
    /// <summary>World SNO to the DPO quest SNOs listed for it in <c>arWorldToDPOQuestList</c>.</summary>
    /// <remarks>The Global's ptContent is a one-element polymorphic array holding GlobalZoneProgressData for zone_progress.</remarks>
    public static Dictionary<int, int[]> WorldQuests(SnoBlob global, DpoLayout l)
    {
        var result = new Dictionary<int, int[]>();
        foreach (int content in global.PolymorphicArray(l.GlobalContentField, l.ZoneProgressSize).Take(1))
            foreach (int entry in global.Array(content + l.WorldQuestListField, l.WorldQuestStride))
                result[global.I32(entry + l.WorldQuestWorldField)] =
                    [.. global.Array(entry + l.WorldQuestQuestsField, DpoLayout.QuestIdSize).Select(global.I32)];
        return result;
    }

    /// <summary>Every (group, id) SNO reference listed in the quest's callbacks (<c>arSnonameTokens</c>, DT_SNO_NAME = group, id).</summary>
    public static IEnumerable<(int Group, int Id)> QuestTokens(SnoBlob quest, DpoLayout l)
    {
        foreach (int phase in quest.PolymorphicArray(l.QuestPhasesField, l.PhaseStride))
        foreach (int set in quest.Array(phase + l.PhaseCallbackSetsField, l.CallbackSetStride))
        foreach (int callback in quest.Array(set + l.SetCallbacksField, l.CallbackStride))
        foreach (int token in quest.Array(callback + l.CallbackTokensField, l.SnoNameStride))
            yield return (quest.I32(token), quest.I32(token + sizeof(int)));
    }
}
