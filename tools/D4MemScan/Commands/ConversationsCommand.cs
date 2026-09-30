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
}
