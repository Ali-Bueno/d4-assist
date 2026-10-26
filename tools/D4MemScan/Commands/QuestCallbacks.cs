using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4MemScan.Commands;

/// <summary>`quest --callbacks`: per phase, every callback's event type, indicator, step world, count, SNO tokens and the
/// polymorphic parameters of its script message map (its own and each event filter's), named by type. Offline; for finding what a
/// phase's callbacks name when the objective reader finds no actor (SNO-valued fields are resolved by name).</summary>
internal static class QuestCallbacks
{
    public static void Print(SnoStore store, TypeSchema schema, int questSno)
    {
        if (store.Meta(schema.SnoGroup("QuestDefinition"), questSno) is not { } quest) return;
        int map = schema.Offset("QuestCallback", "tScriptMessageMap");
        int index = 0;
        foreach (int phase in quest.PolymorphicArray(schema.Offset("QuestDefinition", "arQuestPhases"), schema.Size("QuestPhase")))
        {
            Console.WriteLine($"phase {index++,2} uid {quest.U32(phase + schema.Offset("QuestPhase", "dwUID"))}:");
            foreach (int set in quest.Array(phase + schema.Offset("QuestPhase", "arCallbackSets"), schema.Size("QuestObjectiveSet")))
            foreach (int callback in quest.Array(set + schema.Offset("QuestObjectiveSet", "arCallbacks"), schema.Size("QuestCallback")))
            {
                var tokens = quest.Array(callback + schema.Offset("QuestCallback", "arSnonameTokens"), schema.Size("DT_SNO_NAME"))
                    .Select(t => $"{quest.I32(t)}:{store.Name(quest.I32(t + 4))}");
                Console.WriteLine($"  callback event {quest.I32(callback + map + schema.Offset("ScriptMessageMap", "eEventType"))} " +
                    $"indicator {quest.I32(callback + schema.Offset("QuestCallback", "eIndicatorType"))} " +
                    $"world {store.Name(quest.I32(callback + schema.Offset("QuestCallback", "snoWorld")))} needed {quest.I32(callback + schema.Offset("QuestCallback", "nNeeded"))} " +
                    $"tokens [{string.Join(", ", tokens)}]");
                Dump(quest, store, schema, "params", callback + map + schema.Offset("ScriptMessageMap", "unk_492729e"));
                foreach (int filter in quest.Array(callback + map + schema.Offset("ScriptMessageMap", "arEventFilters"), schema.Size("ScriptMessageMapFilter")))
                    Dump(quest, store, schema, $"filter key {quest.U32(filter + schema.Offset("ScriptMessageMapFilter", "tKey")):X}", filter + schema.Offset("ScriptMessageMapFilter", "pMessageFilter"));
            }
        }
    }

    private static void Dump(SnoBlob quest, SnoStore store, TypeSchema schema, string label, int array)
    {
        string? Type(int p) => p + 12 <= quest.Length ? schema.TypeName(quest.U32(p + schema.Offset("ScriptEventParamBase", "dwType"))) : null;
        var elements = quest.PolymorphicElements(array, p => Type(p) is { } t ? schema.Size(t) : null);
        if (elements is null) { Console.WriteLine($"    {label}: unreadable"); return; }
        foreach (int p in elements)
        {
            string type = Type(p)!;
            int first = p + schema.Size("ScriptEventParamBase"), words = (schema.Size(type) - schema.Size("ScriptEventParamBase")) / 4;
            Console.WriteLine($"    {label}: {type} {string.Join(" ", Enumerable.Range(0, Math.Max(words, 0)).Select(i => $"{quest.I32(first + 4 * i)}({store.Name(quest.I32(first + 4 * i))})"))}");
        }
    }
}
