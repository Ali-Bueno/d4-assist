using D4Memory.Safety;
using D4Memory.SceneRoute;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `layout`: checks the structure layout against the live game (LayoutDiscovery, research/client-memory.md §6). It runs
/// twice: with the known layout, which should read "as known" on a build it was made for, and with every field shifted
/// by --shift bytes, which must find the real fields again. After a game update, the first run names any field that moved.
/// </summary>
internal static class LayoutCommand
{
    private const int DefaultShift = 0x10;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        int shift = reader.Get("--shift") is { } s ? Convert.ToInt32(s.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16) : DefaultShift;

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var (worldSno, worldName) = WorldResolver.Resolve(store, schema, install);
            Console.WriteLine($"World: {worldName} -> SNO {worldSno}.");
            var actorSnos = store.AllEntries(schema.SnoGroup("ActorDefinition")).Select(e => e.Id).ToHashSet();

            using var process = ProcessReader.OpenDiabloIv();
            if (!PlayerRecordLocator.TryLocateLive(process, store, schema, exePath, worldSno, out _, out var player)) return 1;
            // The game maps its image twice; the game object can be in another copy than the player record.
            var (offsets, module) = Offsets.DiscoverAny(process, exePath);
            if (offsets?.Acd is not { } acd) return 1;

            Check("Known layout", offsets);
            var shifted = offsets with
            {
                ActorFields = offsets.ActorFields with
                {
                    AcdId = offsets.ActorFields.AcdId + shift, ActorSno = offsets.ActorFields.ActorSno + shift,
                    Position = offsets.ActorFields.Position + shift,
                },
                Acd = acd with
                {
                    Fields = new AcdFields(acd.Fields.RActorId + shift, acd.Fields.ActorSno + shift, acd.Fields.Position + shift,
                        acd.Fields.Attributes + shift),
                },
            };
            Check($"Every field shifted by 0x{shift:X} (each must read MOVED back to the known offset)", shifted);
            return 0;

            void Check(string title, GameBuildOffsets layout)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                LayoutDiscovery.CheckLive(process, module.Base, layout, player.Position, player.ActorSno, actorSnos.Contains,
                    out string report, out bool conclusive);
                Console.WriteLine($"{title}: {(conclusive ? "conclusive" : "NOT conclusive")}, {clock.ElapsedMilliseconds} ms");
                foreach (var line in report.Split("; ")) Console.WriteLine($"  {line}");
            }
        }
    }
}
