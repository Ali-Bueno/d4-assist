using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Safety;

namespace D4MemScan.Commands;

/// <summary>
/// `markers`: the live game's whole map-marker list (MapMarkerReader, research/dungeon-objectives.md §19): kind, actor, SNO,
/// spot, LevelArea and its region (ZoneCatalog), in --locale (default deDE). --match REGEX keeps the actors whose name matches;
/// --watch S then prints each marker that appears or goes for S seconds (live events: helltide, world boss, legion).
/// </summary>
internal static class MarkersCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, install) = GameData.Open(reader);
        string locale = reader.Get("--locale") ?? "deDE";
        var match = reader.Get("--match") is { } pattern ? new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;
        using (store)
        {
            var zones = new ZoneCatalog(store, schema);
            string Describe(MapMarker m) =>
                $"kind {m.Kind,2} {store.Name(m.ActorSno) ?? "?",-56} {m.ActorSno,8} ({m.Position.X:F1},{m.Position.Y:F1},{m.Position.Z:F1}) " +
                $"{store.Name(m.LevelAreaSno) ?? m.LevelAreaSno.ToString()} [{zones.Name(m.LevelAreaSno, locale) ?? "-"}]";
            bool Kept(MapMarker m) => match is null || match.IsMatch(store.Name(m.ActorSno) ?? "");

            using var process = ProcessReader.OpenDiabloIv();
            var (offsets, module) = Offsets.DiscoverAny(process, Path.Combine(install, GameData.MainModuleFileName));
            if (offsets is null) { Console.WriteLine("Game object not found."); return 1; }
            if (MapMarkerReader.Locate(process, module.Base, offsets.GameObjectPointerRva) is not { } found) { Console.WriteLine("Marker list not found."); return 1; }
            long head = found;
            if (MapMarkerReader.TryRead(process, head) is not { } markers) { Console.WriteLine("Marker list unreadable."); return 1; }

            foreach (var m in markers.Where(Kept).OrderBy(m => m.Kind).ThenBy(m => store.Name(m.ActorSno))) Console.WriteLine(Describe(m));
            Console.WriteLine($"{markers.Count} marker(s), {markers.Count(Kept)} shown; head 0x{head:X}.");

            if (reader.Get("--watch") is not { } watch) return 0;
            var seen = markers.Where(Kept).ToHashSet();
            var until = DateTime.Now.AddSeconds(int.Parse(watch));
            while (DateTime.Now < until)
            {
                Thread.Sleep(1000);
                if (MapMarkerReader.TryRead(process, head) is not { } now)
                {
                    // The client rebuilds the list on a world or area change (live 2026-10-25, walking out of Zarbinzet): find it again.
                    if (MapMarkerReader.Locate(process, module.Base, offsets.GameObjectPointerRva) is { } moved && MapMarkerReader.TryRead(process, moved) is { } list)
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss} list moved to 0x{moved:X}");
                        (head, now) = (moved, list);
                    }
                    else { Console.WriteLine($"{DateTime.Now:HH:mm:ss} list gone, looking again"); continue; }
                }
                var current = now.Where(Kept).ToHashSet();
                foreach (var m in current.Except(seen)) Console.WriteLine($"{DateTime.Now:HH:mm:ss} + {Describe(m)}");
                foreach (var m in seen.Except(current)) Console.WriteLine($"{DateTime.Now:HH:mm:ss} - {Describe(m)}");
                seen = current;
            }
        }
        return 0;
    }
}
