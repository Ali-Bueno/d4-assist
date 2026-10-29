using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using D4Assist.Navigation;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Safety;
using D4Memory.SceneRoute;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `acd`: finds the ACD (game record, research/client-memory.md §4) of nearby gizmos or of actors matching a name,
/// prints their attributes by name, and optionally watches the nearest one's attributes and slot words.
/// </summary>
internal static class AcdCommand
{
    /// <summary>Default listing radius (world units): the gizmos around the player, not the whole loaded window.</summary>
    private const float DefaultRangeUnits = 30f;
    private const int DefaultIntervalMs = 250;
    private const int MaxConsoleLines = 40;

    /// <summary>RActor +0x0C value of a client-only actor, which has no ACD.</summary>
    private const uint NoAcdId = 0xFFFFFFFF;
    private const uint MaxAttributeId = 0x1000; // the live table held ids up to 0x571

    private sealed record AcdLine(
        float Distance, string Direction, int ActorSno, string? Name, int? GizmoType, uint RActorId, uint AcdId,
        long AcdSlotAddress, SortedDictionary<string, string> Attributes);

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        float range = float.TryParse(reader.Get("--range"), CultureInfo.InvariantCulture, out var r) ? r : DefaultRangeUnits;
        int intervalMs = int.TryParse(reader.Get("--interval-ms"), out var i) ? i : DefaultIntervalMs;
        int watchSeconds = int.TryParse(reader.Get("--watch"), out var w) ? w : 0;
        var match = reader.Get("--match") is { } pattern ? new Regex(pattern, RegexOptions.IgnoreCase) : null;
        int[] slotWords = reader.Get("--slot-words") is { } words
            ? [.. words.Split(',').Select(w => Convert.ToInt32(w.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16))]
            : [];

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var (worldSno, worldName) = WorldResolver.Resolve(store, schema, install);
            Console.WriteLine($"World: {worldName} -> SNO {worldSno}.");

            using var process = ProcessReader.OpenDiabloIv();
            if (!PlayerRecordLocator.TryLocateLive(process, store, schema, exePath, worldSno, out var module, out var player)) return 1;
            // The game maps its image twice; the game object can be in another copy than the player record.
            (var discovered, module) = Offsets.DiscoverAny(process, exePath);
            if (discovered is not { Acd: not null } offsets)
            {
                Console.WriteLine("ACD addresses not found for this build.");
                return 1;
            }
            if (!RActorReader.TryRead(process, module.Base, offsets, out var actors) || !AcdReader.TryOpen(process, module.Base, offsets, out var acds))
            {
                Console.Error.WriteLine("RActor or ACD route unavailable (container header unreadable or inconsistent).");
                return 1;
            }
            var names = new AttributeNameTable(process, module.Base, offsets.Acd);
            if (reader.Get("--names") is { } namePattern)
            {
                var nameMatch = new Regex(namePattern, RegexOptions.IgnoreCase);
                for (uint id = 0; id < MaxAttributeId; id++)
                    if (names.NameOf(id) is { } name && nameMatch.IsMatch(name)) Console.WriteLine($"0x{id:X3} {name}");
                return 0;
            }
            var defs = new ActorDefinitionReader(store, schema);

            // --all: every occupied ACD slot, also those with no placed RActor (items in the inventory or stash), by SNO name.
            if (reader.Has("--all"))
            {
                var all = acds.All()
                    .Select(acd => (Acd: acd, Name: store.Name(acd.ActorSno)))
                    .Where(c => match is null || c.Name is { } n && match.IsMatch(n)).ToList();
                PrintAndSave([.. all.Select(c => new AcdLine(Vector3.Distance(c.Acd.Position, player.Position), "-", c.Acd.ActorSno, c.Name,
                    defs.Read(c.Acd.ActorSno)?.GizmoType, c.Acd.RActorId, c.Acd.Id, c.Acd.SlotAddress,
                    new SortedDictionary<string, string>(AcdWatch.Attributes(process, names, c.Acd))))], float.PositiveInfinity);
                return 0;
            }

            // Without --match: every gizmo (eActorGizmoType >= 0), the actors whose "used" state is wanted.
            var candidates = actors
                .Where(a => a.IsPlaced && a.AcdId != NoAcdId && float.IsFinite(a.Position.X) && Vector3.Distance(a.Position, player.Position) <= range)
                .Select(a => (Actor: a, Name: store.Name(a.ActorSno), Def: defs.Read(a.ActorSno)))
                .Where(c => match is not null ? c.Name is { } n && match.IsMatch(n) : c.Def?.GizmoType >= 0)
                .OrderBy(c => Vector3.Distance(c.Actor.Position, player.Position)).ToList();

            var lines = new List<AcdLine>();
            var records = new List<AcdRecord>();
            int disagree = 0, missing = 0;
            foreach (var (actor, name, def) in candidates)
            {
                if (!acds.TryGet(actor.AcdId, out var acd)) { missing++; continue; }
                if (acd.RActorId != actor.Id || acd.ActorSno != actor.ActorSno) { disagree++; continue; }
                var delta = actor.Position - player.Position;
                var attributes = new SortedDictionary<string, string>(AcdWatch.Attributes(process, names, acd));
                var word = new byte[sizeof(uint)];
                foreach (int off in slotWords)
                    attributes[$"+0x{off:X}"] = process.TryRead(acd.SlotAddress + off, word, word.Length) ? $"0x{BitConverter.ToUInt32(word):X}" : "?";
                lines.Add(new AcdLine(delta.Length(), ScreenDirections.Bin(delta.X, delta.Y).ToString(), actor.ActorSno, name, def?.GizmoType,
                    actor.Id, acd.Id, acd.SlotAddress, attributes));
                records.Add(acd);
            }
            if (candidates.Count > 0)
                Console.WriteLine($"[{(lines.Count > 0 && disagree == 0 ? "PASS" : "FAIL")}] {candidates.Count} matched actor(s) with an ACD id: " +
                                  $"{lines.Count} ACD slot(s) hold the same RActor id and SNO, {disagree} disagree, {missing} not in the pool");
            PrintAndSave(lines, range);

            if (watchSeconds > 0 && lines.Count > 0)
                AcdWatch.Run(process, acds, names, records[0], $"{lines[0].Name ?? "?"}, {lines[0].Distance:F1} units {lines[0].Direction}", watchSeconds, intervalMs);
            else if (watchSeconds > 0)
                Console.WriteLine("Watch: no matching actor with an ACD nearby.");
            return 0;
        }
    }

    private static void PrintAndSave(List<AcdLine> lines, float range)
    {
        Console.WriteLine($"ACDs within {range} units: {lines.Count}");
        foreach (var l in lines.Take(MaxConsoleLines))
            Console.WriteLine($"  {l.Distance,5:F1} {l.Direction,-9} {l.Name ?? "?"} (gizmo {l.GizmoType?.ToString() ?? "?"}) " +
                              $"ractor 0x{l.RActorId:X} acd 0x{l.AcdId:X}: {string.Join(" ", l.Attributes.Select(a => $"{a.Key}={a.Value}"))}");
        if (lines.Count > MaxConsoleLines) Console.WriteLine($"  ... {lines.Count - MaxConsoleLines} more (see JSON)");

        string path = Path.Combine(Path.GetTempPath(), $"d4memscan-acd-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        JsonOutput.Write(lines, path);
        Console.WriteLine($"Full list: {path}");
    }
}
