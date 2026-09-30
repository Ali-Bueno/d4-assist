using System.Numerics;
using D4Assist.Navigation;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.SceneRoute;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `actors`: reads the RActor pool via the static route (research/client-memory.md §4), validates it against
/// the player's static record, lists nearby placed actors and optionally watches one slot for changes.
/// </summary>
internal static class ActorsCommand
{
    /// <summary>Default listing radius (world units); roughly two 32-unit tiles, the loaded window around the player.</summary>
    private const float DefaultRangeUnits = 60f;
    private const int DefaultIntervalMs = 250;
    private const int MaxConsoleLines = 40;
    private const string PrisonerNameFragment = "Prisoner";

    // Slot words that changed when a hollow-tree clicky was used (live 2026-09-29, research/client-memory.md §4); printed for gizmos.
    private static readonly int[] UseCandidateOffsets = [0x78, 0xA0, 0x140, 0x148, 0x230];

    private sealed record ActorLine(
        float Distance, string Direction, int ActorSno, string? Name, int? EType, int? GizmoType,
        uint Id, uint AcdId, long SlotAddress, float X, float Y, float Z, string? UseWords = null);

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        float range = float.TryParse(reader.Get("--range"), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : DefaultRangeUnits;
        int intervalMs = int.TryParse(reader.Get("--interval-ms"), out var i) ? i : DefaultIntervalMs;
        int watchSeconds = int.TryParse(reader.Get("--watch"), out var w) ? w : 0;
        var watchMatch = reader.Get("--match") is { } pattern ? new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;
        // --words 0x148,0x1F4 adds slot words to the printed ones, to compare used and untouched gizmos in one snapshot.
        int[] wordOffsets = [.. UseCandidateOffsets, .. (reader.Get("--words") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => Convert.ToInt32(w.Trim(), 16))];

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var (worldSno, worldName) = WorldResolver.Resolve(store, schema, install);
            Console.WriteLine($"World: {worldName} -> SNO {worldSno}.");
            var sceneSnos = PlayerRecordLocator.SceneSnos(store, schema);
            var playerActorSnos = PlayerRecordLocator.PlayerActorSnos(store, schema);

            using var process = ProcessReader.OpenDiabloIv();
            var (regions, modules) = PlayerRecordLocator.LocateModules(process, exePath);
            var (liveModule, hits) = PlayerRecordLocator.ScanModules(process, regions, modules, playerActorSnos, worldSno, sceneSnos);
            if (liveModule is not { } module || hits.FirstOrDefault(PlayerRecordMatcher.IsLive) is not { PositionAddress: not 0 } player)
            {
                Console.Error.WriteLine("No live player record found.");
                return 1;
            }
            if (Offsets.Discover(process, module) is not { } offsets) return 1;
            if (!RActorReader.TryRead(process, module.Base, offsets, out var actors))
            {
                Console.Error.WriteLine("RActor route unavailable (container header unreadable or inconsistent).");
                return 1;
            }
            Console.WriteLine($"RActor pool: {actors.Count} occupied slot(s), {actors.Count(a => a.IsPlaced)} placed.");

            var playerActor = ActorsValidation.Run(process, module.Base, offsets, actors, player);
            var defs = new ActorDefinitionReader(store, schema);
            var lines = actors.Where(a => a.IsPlaced && a.Id != playerActor?.Id && IsFinite(a.Position))
                .Select(a => ToLine(a, player.Position, store.Name(a.ActorSno), defs.Read(a.ActorSno)))
                .Where(l => l.Distance <= range).OrderBy(l => l.Distance)
                .Select(l => l.GizmoType >= 0 ? l with { UseWords = UseWords(process, l.SlotAddress, wordOffsets) } : l).ToList();
            PrintAndSave(lines, range);

            if (watchSeconds > 0 && reader.Has("--watch-player") && playerActor is { } self)
                WatchPlayer(process, module.Base, offsets, self, watchSeconds, intervalMs);
            else if (watchSeconds > 0) Watch(process, module.Base, offsets, actors, lines, watchSeconds, intervalMs, watchMatch);
            return 0;
        }
    }

    private static ActorLine ToLine(RActorRecord a, Vector3 player, string? name, ActorDefinitionInfo? def)
    {
        var delta = a.Position - player;
        string direction = ScreenDirections.Bin(delta.X, delta.Y).ToString();
        return new ActorLine(Vector3.Distance(a.Position, player), direction, a.ActorSno, name, def?.EType, def?.GizmoType,
            a.Id, a.AcdId, a.SlotAddress, a.Position.X, a.Position.Y, a.Position.Z);
    }

    private static void PrintAndSave(List<ActorLine> lines, float range)
    {
        Console.WriteLine($"Placed actors within {range} units: {lines.Count}");
        foreach (var l in lines.Take(MaxConsoleLines))
            Console.WriteLine($"  {l.Distance,6:F1} {l.Direction,-9} sno {l.ActorSno} {l.Name ?? "?"} eType {l.EType?.ToString() ?? "?"} gizmo {l.GizmoType?.ToString() ?? "?"}{(l.UseWords is { } w ? " " + w : "")}");
        if (lines.Count > MaxConsoleLines) Console.WriteLine($"  ... {lines.Count - MaxConsoleLines} more (see JSON)");

        string path = Path.Combine(Path.GetTempPath(), $"d4memscan-actors-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        JsonOutput.Write(lines, path);
        Console.WriteLine($"Full list: {path}");
    }

    private static void Watch(
        ProcessReader process, long moduleBase, GameBuildOffsets offsets, IReadOnlyList<RActorRecord> actors,
        List<ActorLine> lines, int seconds, int intervalMs, System.Text.RegularExpressions.Regex? match)
    {
        // --match picks the nearest actor whose name matches; otherwise the nearest gizmo or prisoner.
        bool IsCandidate(ActorLine l) => match is not null ? l.Name is { } n && match.IsMatch(n)
            : l.GizmoType >= 0 || l.Name?.Contains(PrisonerNameFragment, StringComparison.OrdinalIgnoreCase) == true;
        var pick = lines.FirstOrDefault(IsCandidate);
        if (pick is null)
        {
            Console.WriteLine("Watch: no nearby gizmo or prisoner actor to watch.");
            return;
        }
        if (!PoolContainer.TryOpen(process, moduleBase, offsets, offsets.Actor, out var pool)) return;

        var target = actors.First(a => a.SlotAddress == pick.SlotAddress);
        ActorsWatch.Run(process, target, $"{pick.Name ?? "?"}, {pick.Distance:F1} units {pick.Direction}", pool.Stride, seconds, intervalMs);
    }

    // The player's own slot, to find what records the gizmo the player just used.
    private static void WatchPlayer(ProcessReader process, long moduleBase, GameBuildOffsets offsets, RActorRecord self, int seconds, int intervalMs)
    {
        if (!PoolContainer.TryOpen(process, moduleBase, offsets, offsets.Actor, out var pool)) return;
        ActorsWatch.Run(process, self, "player", pool.Stride, seconds, intervalMs, maxLinesPerBurst: int.MaxValue);
    }

    private static string UseWords(ProcessReader process, long slot, int[] offsets)
    {
        var word = new byte[sizeof(uint)];
        return string.Join(" ", offsets.Select(o =>
            process.TryRead(slot + o, word, word.Length) ? $"+0x{o:X}={BitConverter.ToUInt32(word):X}" : $"+0x{o:X}=?"));
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
