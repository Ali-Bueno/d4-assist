using System.Text.RegularExpressions;
using D4Data.Readers;

namespace D4MemScan.Commands;

/// <summary>`gizmos --type N` / `gizmos --match <name-regex>`: every actor whose ActorDefinition.eActorGizmoType is N, or whose
/// name matches, by name (offline census). `--match` also prints eType, gizmo type, default team and fIsNPC.</summary>
internal static class GizmosCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, _) = GameData.Open(reader);
        string? pattern = reader.Get("--match");
        int? type = pattern is null || reader.Has("--type") ? int.Parse(reader.Require("--type")) : null;
        var match = pattern is null ? null : new Regex(pattern, RegexOptions.IgnoreCase);
        using (store)
        {
            var definitions = new ActorDefinitionReader(store, schema);
            var names = store.AllEntries(schema.SnoGroup("ActorDefinition"))
                .Where(e => match?.IsMatch(e.Name) != false)
                .Select(e => (e, d: definitions.Read(e.Id)))
                .Where(x => x.d is { } d && (type is null || d.GizmoType == type))
                .Select(x => match is null ? $"{x.e.Name} ({x.e.Id})"
                    : $"{x.e.Name} ({x.e.Id}) eType {x.d!.Value.EType} gizmo {x.d.Value.GizmoType} team {x.d.Value.DefaultTeam} npc {x.d.Value.IsNpc}")
                .Order().ToList();
            Console.WriteLine($"{names.Count} actor(s) {(type is { } t ? $"with gizmo type {t}" : "")}{(match is null ? "" : $" matching {pattern}")}:");
            foreach (string name in names) Console.WriteLine("  " + name);
        }
        return 0;
    }
}
