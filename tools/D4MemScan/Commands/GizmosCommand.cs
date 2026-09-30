using D4Data.Readers;

namespace D4MemScan.Commands;

/// <summary>`gizmos --type N`: every actor whose ActorDefinition.eActorGizmoType is N, by name (offline census).</summary>
internal static class GizmosCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, _) = GameData.Open(reader);
        int type = int.Parse(reader.Require("--type"));
        using (store)
        {
            var definitions = new ActorDefinitionReader(store, schema);
            var names = store.AllEntries(schema.SnoGroup("ActorDefinition"))
                .Where(e => definitions.Read(e.Id) is { } d && d.GizmoType == type).Select(e => $"{e.Name} ({e.Id})").Order().ToList();
            Console.WriteLine($"{names.Count} actor(s) with gizmo type {type}:");
            foreach (string name in names) Console.WriteLine("  " + name);
        }
        return 0;
    }
}
