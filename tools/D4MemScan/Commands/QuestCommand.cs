using System.Collections.Concurrent;
using System.Diagnostics;
using D4Data.Readers;
using D4Memory.Pools;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `quest --sno N`: a quest's phases (dwUID, in order) and the objects each phase's scripts name (offline).
/// `--callbacks` also prints each phase's callbacks and their script parameters (QuestCallbacks); `--group G` the markers of an actor
/// group; `--actors R` the actors matching R with team, NPC flag and family; `--world-actors R` the actors placed in marker sets matching R.
/// `--watch S` also finds, in the live game's memory, every u32 phase UID of that quest within ±0x100 bytes of the quest
/// SNO, then prints each one that changes for S seconds: advance the quest while it runs to find the current phase.
/// </summary>
internal static class QuestCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, install) = GameData.Open(reader);
        int sno = int.Parse(reader.Require("--sno"));
        using (store)
        {
            var quests = new QuestScriptMarkerReader(store, schema, new MarkerReader(store, schema));
            var phases = quests.PhaseObjects(sno);
            int index = 0;
            foreach (var (uid, markers, actors, sceneMarkers, subQuests, worlds, kindMarkers, hidden) in phases)
                Console.WriteLine($"phase {index++,2} uid {uid} (0x{uid:X}): " +
                    string.Join(", ", markers.Select(m => $"{store.Name(m.Sno)} ({m.Position.X:F0},{m.Position.Y:F0}){(hidden?.Contains(m) == true ? " hidden" : "")}")
                        .Concat(sceneMarkers.Select(s => $"{store.Name(s.Marker.Sno)} ({s.Marker.Position.X:F0},{s.Marker.Position.Y:F0} in {store.Name(s.SceneSno)})"))
                        .Concat((kindMarkers ?? []).Select(k => $"{store.Name(k.Marker.Sno)} (blueprint {k.Marker.Position.X:F0},{k.Marker.Position.Y:F0})"))
                        .Concat(actors.Select(a => $"{store.Name(a)} (any)"))
                        .Concat((subQuests ?? new HashSet<int>()).Select(q => $"waits on quest {q} {store.Name(q)}"))
                        .Concat((worlds ?? new HashSet<int>()).Select(w => $"shown in {store.Name(w)}"))));
            if (reader.Has("--callbacks")) QuestCallbacks.Print(store, schema, sno);
            if (reader.Get("--group") is { } gbid) PrintGroup(store, schema, uint.Parse(gbid));
            if (reader.Get("--actors") is { } pattern) PrintActors(store, schema, pattern);
            if (reader.Get("--world-actors") is { } world) PrintWorldActors(store, schema, world);
            var uids = phases.Select(p => p.PhaseUid).ToHashSet();
            if (reader.Has("--find")) Find(sno, uids, Path.Combine(install, GameData.MainModuleFileName));
            if (reader.Get("--watch") is { } watch) Watch(sno, uids, int.Parse(watch));
        }
        return 0;
    }

    // `--group G`: every marker of an actor group (GBID, as a callback's ScriptEventParamActorGroup names it), by marker set.
    private static void PrintGroup(D4Data.Casc.SnoStore store, D4Data.Schema.TypeSchema schema, uint gbid)
    {
        var markers = new MarkerReader(store, schema);
        foreach (var (set, name) in store.AllEntries(schema.SnoGroup("MarkerSetDefinition")))
            foreach (var (marker, groups) in markers.ActorGroupMarkers(set))
                if (groups.Contains(gbid))
                    Console.WriteLine($"group {gbid}: {store.Name(marker.Sno)} ({marker.Sno}) at ({marker.Position.X:F0},{marker.Position.Y:F0}) in {name}");
    }

    // `--actors R`: the actors whose name matches R with their kind, team, NPC flag and monster family (what NpcClassifier reads).
    private static void PrintActors(D4Data.Casc.SnoStore store, D4Data.Schema.TypeSchema schema, string pattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var definitions = new ActorDefinitionReader(store, schema);
        foreach (var (id, name) in store.AllEntries(schema.SnoGroup("ActorDefinition")).Where(e => regex.IsMatch(e.Name)))
            if (definitions.Read(id) is { } d)
                Console.WriteLine($"actor {name} ({id}): type {d.EType} npc {d.IsNpc} team {d.DefaultTeam} family {store.Name(d.MonsterFamilySno)}");
    }

    // `--world-actors R`: the NPC and monster actors placed in the marker sets whose name matches R, with the PrintActors facts.
    private static void PrintWorldActors(D4Data.Casc.SnoStore store, D4Data.Schema.TypeSchema schema, string world)
    {
        var markers = new MarkerReader(store, schema);
        var definitions = new ActorDefinitionReader(store, schema);
        int actorGroup = schema.SnoGroup("ActorDefinition");
        foreach (var (set, name) in store.AllEntries(schema.SnoGroup("MarkerSetDefinition")).Where(e => System.Text.RegularExpressions.Regex.IsMatch(e.Name, world, System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            foreach (var actor in markers.TopLevel(set).Values.Where(m => m.Group == actorGroup).GroupBy(m => m.Sno))
                if (definitions.Read(actor.Key) is { } d)
                    Console.WriteLine($"placed {store.Name(actor.Key)} ({actor.Key}) x{actor.Count()} in {name}: npc {d.IsNpc} team {d.DefaultTeam} family {store.Name(d.MonsterFamilySno)}");
    }

    // The pairs the app reads (D4Memory.Actors.QuestPhaseLocator), in each module view's data.
    private static void Find(int sno, HashSet<uint> uids, string exePath)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var regions = process.ImageRegions();
        foreach (var module in ModuleLocator.LocateAll(regions, process, exePath))
            foreach (long at in D4Memory.Actors.QuestPhaseLocator.Find(process, ModuleLocator.WritableRegionsAt(regions, module.Base), sno, uids))
                Console.WriteLine($"Module 0x{module.Base:X}: phase at RVA 0x{at - module.Base:X} = {D4Memory.Actors.QuestPhaseLocator.Read(process, at, sno, uids)}");
    }

    private const int Around = 0x100;

    private static void Watch(int sno, HashSet<uint> uids, int seconds)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var clock = Stopwatch.StartNew();
        var candidates = new ConcurrentBag<long>();
        var offsets = new ConcurrentDictionary<long, int>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(process.Regions(), options, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, window) in RegionWindows.ReadRegion(process, region, buffer))
            {
                var span = window.Span;
                for (int i = 0; i + 4 <= span.Length; i += 4)
                {
                    if (BitConverter.ToInt32(span[i..]) != sno) continue;
                    for (int d = Math.Max(-i, -Around) & ~3; d <= Around && i + d + 4 <= span.Length; d += 4)
                        if (d != 0 && uids.Contains(BitConverter.ToUInt32(span[(i + d)..]))) { candidates.Add(address + i + d); offsets.TryAdd(address + i + d, d); }
                }
            }
            return buffer;
        }, _ => { });
        var watched = candidates.Distinct().Order().ToList();
        Console.WriteLine($"{watched.Count} phase-UID word(s) near the quest SNO ({clock.ElapsedMilliseconds} ms). Advance the quest now.");

        var last = watched.ToDictionary(a => a, a => PoolContainer.TryReadU32(process, a, out uint v) ? v : (uint?)null);
        foreach (var group in watched.GroupBy(a => last[a]).OrderByDescending(g => g.Count()))
            Console.WriteLine($"  phase {group.Key}: {group.Count()} word(s), e.g. {string.Join(" ", group.Select(a => $"0x{a:X}({offsets[a]:+0;-0})"))}");
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds)
        {
            foreach (long a in watched)
            {
                uint? now = PoolContainer.TryReadU32(process, a, out uint v) ? v : null;
                if (now == last[a]) continue;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} 0x{a:X}: {last[a]} -> {now}{(now is { } n && uids.Contains(n) ? " (phase)" : "")}");
                last[a] = now;
            }
            Thread.Sleep(250);
        }
    }
}
