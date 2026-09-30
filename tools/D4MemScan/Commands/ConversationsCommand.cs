using System.Diagnostics;
using D4Memory.Safety;
using D4Memory.SceneRoute;
using D4Memory.Ui;

namespace D4MemScan.Commands;

/// <summary>
/// `conversations`: searches the module data for the playing-conversation list and the dialogue menu
/// (D4Memory.Ui.ConversationDiscovery) every few seconds until both are found, then prints what they hold on each change.
/// Talk to an NPC while it runs: each search only succeeds while its state is live.
/// </summary>
internal static class ConversationsCommand
{
    private const int DefaultSeconds = 180;
    private const int SearchIntervalMs = 2000, ReadIntervalMs = 250;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, install) = GameData.Open(reader);
        int seconds = int.Parse(reader.Get("--watch") ?? DefaultSeconds.ToString());
        using (store)
        {
            int group = schema.SnoGroup("ConversationDefinition");
            var names = store.AllEntries(group).ToDictionary(e => e.Id, e => e.Name);
            bool IsConversation(int sno) => names.ContainsKey(sno);
            string Name(int sno) => names.TryGetValue(sno, out var n) ? n : sno.ToString();

            using var process = ProcessReader.OpenDiabloIv();
            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var (offsets, module) = Offsets.DiscoverAny(process, exePath);
            if (offsets is null) return 1;
            var range = new ModuleRange(module.Base, module.SizeOfImage);
            var data = ModuleLocator.WritableRegionsAt(process.ImageRegions(), module.Base);
            var layout = GameBuildOffsetsTable.ConversationLayout;

            if (reader.Has("--menu-probe")) { MenuProbe(process, data, range, IsConversation, Name); return 0; }

            long? playing = null, menu = null;
            string last = "";
            var clock = Stopwatch.StartNew();
            long nextSearch = 0;
            Console.WriteLine($"{names.Count} conversations known. Watching for {seconds}s.");
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if ((playing is null || menu is null) && clock.ElapsedMilliseconds >= nextSearch)
                {
                    var search = Stopwatch.StartNew();
                    playing ??= Report("playing list", ConversationDiscovery.FindPlayingList(process, data, range, layout, IsConversation));
                    menu ??= Report("dialogue menu", ConversationDiscovery.FindMenuOptions(process, data, range, layout, IsConversation));
                    nextSearch = clock.ElapsedMilliseconds + SearchIntervalMs;
                    if (playing is null || menu is null) Console.Write($"\rsearching ({search.ElapsedMilliseconds} ms)   ");
                }
                if (playing is not null || menu is not null)
                {
                    var found = layout with { PlayingListRva = (playing ?? 0) - module.Base, OptionsPointerRva = (menu ?? 0) - module.Base };
                    if (ConversationReader.TryRead(process, module.Base, found) is { } snapshot)
                    {
                        string now = $"menu=[{string.Join(", ", snapshot.MenuOptions.Select(Name))}] playing=[{string.Join(", ", snapshot.Playing.Select(Name))}]";
                        if (now != last) Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {now}");
                        last = now;
                    }
                }
                Thread.Sleep(ReadIntervalMs);
            }

            long? Report(string what, long? at)
            {
                if (at is { } a) Console.WriteLine($"\n{DateTime.Now:HH:mm:ss.fff} {what} at RVA 0x{a - module.Base:X}");
                return at;
            }
        }
        return 0;
    }

    // Loose dialogue-menu search, for when the strict one fails: any data pointer followed by a count 1..32 whose array,
    // at some stride and field offset, holds at least one conversation SNO. Prints every candidate.
    private static void MenuProbe(ProcessReader process, IReadOnlyList<D4Memory.Scan.MemoryRegion> data, ModuleRange module,
        Func<int, bool> isConversation, Func<int, string> name)
    {
        int[] strides = [0x8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x60];
        var buffer = new byte[D4Memory.Scan.RegionWindows.DefaultWindowBytes];
        int shown = 0;
        foreach (var region in data)
            foreach (var (address, window) in D4Memory.Scan.RegionWindows.ReadRegion(process, region, buffer))
            {
                var bytes = window.ToArray();
                for (int i = (int)((8 - address % 8) % 8); i + 12 <= bytes.Length; i += 8)
                {
                    ulong ptr = BitConverter.ToUInt64(bytes, i);
                    uint count = BitConverter.ToUInt32(bytes, i + 8);
                    if (ptr is < 0x10000 or > 0x7FFF_FFFF_0000 || module.Contains((long)ptr) || count is 0 or > 32) continue;
                    var array = new byte[(int)count * 0x60];
                    if (!process.TryRead((long)ptr, array, array.Length)) continue;
                    foreach (int stride in strides)
                        for (int field = 0; field + 4 <= stride; field += 4)
                        {
                            var snos = Enumerable.Range(0, (int)count).Select(e => BitConverter.ToInt32(array, e * stride + field)).ToList();
                            if (!snos.Any(isConversation)) continue;
                            if (shown++ < 5000)
                                Console.WriteLine($"RVA 0x{address + i - module.Base:X} count {count} stride 0x{stride:X} field 0x{field:X}: {string.Join(", ", snos.Select(name))}");
                        }
                }
            }
        Console.WriteLine($"{shown} candidate(s).");
    }
}
