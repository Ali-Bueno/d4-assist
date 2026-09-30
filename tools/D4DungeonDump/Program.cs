using System.Numerics;
using System.Text.Json;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Model;
using D4Data.Readers;
using D4Data.Schema;

namespace D4DungeonDump;

/// <summary>Diagnostic dumper: World SNO -> placed scenes, scene bounds/exits/nav cells, gameplay markers (JSON).</summary>
internal static class Program
{
    private const string Usage =
        "usage: D4DungeonDump <world name|id> --defs <d4data definitions.json> [--install <Diablo IV dir>]\n" +
        "                     [--tileset <scene name prefix>] [--scene <name|id>]... [--all-layers] [--grid] [--out <file.json>]";

    private static int Main(string[] args)
    {
        try
        {
            var opt = Options.Parse(args);
            var schema = TypeSchema.Load(opt.DefinitionsPath);
            using var store = new SnoStore(opt.InstallPath);
            var result = Dump(store, schema, opt);
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            if (opt.OutPath is null) Console.WriteLine(json);
            else File.WriteAllText(opt.OutPath, json);
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Usage);
            return 1;
        }
    }

    private static DumpResult Dump(SnoStore store, TypeSchema schema, Options opt)
    {
        var worlds = new WorldReader(store, schema);
        var scenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), opt.AllLayers, opt.Grid);
        var (world, chunks) = worlds.Read(store.Resolve(worlds.Group, opt.World));

        // Scene definitions: every placed scene plus any tiles requested explicitly.
        var sceneIds = chunks.Select(c => c.SceneSno).ToList();
        if (opt.Tileset is not null) sceneIds.AddRange(store.EntriesWithPrefix(scenes.Group, opt.Tileset).Select(e => e.Id));
        sceneIds.AddRange(opt.Scenes.Select(s => store.Resolve(scenes.Group, s)));
        var definitions = sceneIds.Distinct().ToDictionary(id => id, scenes.Read);

        var placed = new List<PlacedScene>();
        var worldMarkers = new List<MarkerInfo>();
        foreach (var c in chunks)
        {
            var def = definitions[c.SceneSno];
            placed.Add(new PlacedScene(def.Scene, new Pose(ToArray(c.Position), [c.Rotation.X, c.Rotation.Y, c.Rotation.Z, c.Rotation.W]),
                c.LevelAreaSno > 0 ? new SnoRef(c.LevelAreaSno, store.Name(c.LevelAreaSno)) : null,
                TransformBox(def.Bounds, c.Position, c.Rotation)));
            worldMarkers.AddRange(def.Markers.Select(m => m with { World = ToArray(ToWorld(m.Local, c.Position, c.Rotation)) }));
        }

        return new DumpResult("D4DungeonDump", store.BuildVersion, world, placed,
            [.. definitions.Values.OrderBy(s => s.Scene.Name, StringComparer.Ordinal)], worldMarkers, Notes(world));
    }

    private static List<string> Notes(WorldInfo world)
    {
        var notes = new List<string>();
        if (world.LayoutType == Conventions.LayoutDrlg)
            notes.Add("DRLG world: the layout is generated server-side, so client data holds no placed scenes. " +
                      "Placements arrive at runtime (RevealSceneMessage: snoScene + PRTransform); use --tileset/--scene to dump tile definitions.");
        notes.Add("Scene-local axes: X = south, Y = east, Z = up (engine compass N = -X, E = +Y). Units: world units.");
        notes.Add("Walkability: coarse walkable box per 32-unit nav cell, walkable area and, with --grid, a 1-unit top-down map " +
                  "('#' walkable, rows north to south) from the 0.5-unit voxel grid (DRLG tiles) or the decoded m_cellBytes surfaces (other scenes).");
        return notes;
    }

    private static Vector3 ToWorld(float[] local, Vector3 position, Quaternion rotation) =>
        Vector3.Transform(new Vector3(local[0], local[1], local[2]), rotation) + position;

    private static Box TransformBox(Box box, Vector3 position, Quaternion rotation)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var x in new[] { box.Min[0], box.Max[0] })
        foreach (var y in new[] { box.Min[1], box.Max[1] })
        foreach (var z in new[] { box.Min[2], box.Max[2] })
        {
            var w = ToWorld([x, y, z], position, rotation);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
        return new Box(ToArray(min), ToArray(max));
    }

    private static float[] ToArray(Vector3 v) => [v.X, v.Y, v.Z];

    private sealed record Options(string World, string DefinitionsPath, string InstallPath, string? Tileset,
        List<string> Scenes, bool AllLayers, bool Grid, string? OutPath)
    {
        public static Options Parse(string[] args)
        {
            string? world = null, defs = Environment.GetEnvironmentVariable("D4DATA_DEFINITIONS"), install = null, tileset = null, output = null;
            var scenes = new List<string>();
            bool allLayers = false, grid = false;
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}.");
                switch (args[i])
                {
                    case "--defs": defs = Next(); break;
                    case "--install": install = Next(); break;
                    case "--tileset": tileset = Next(); break;
                    case "--scene": scenes.Add(Next()); break;
                    case "--all-layers": allLayers = true; break;
                    case "--grid": grid = true; break;
                    case "--out": output = Next(); break;
                    default:
                        if (args[i].StartsWith("--", StringComparison.Ordinal) || world is not null)
                            throw new ArgumentException($"Unexpected argument '{args[i]}'.");
                        world = args[i];
                        break;
                }
            }
            if (world is null) throw new ArgumentException("Missing world name or id.");
            if (defs is null) throw new ArgumentException("Missing --defs (or D4DATA_DEFINITIONS).");
            if (install is null && !SnoStore.TryLocateInstall(out install))
                throw new ArgumentException("Diablo IV install not found; pass --install.");
            return new Options(world, defs, install, tileset, scenes, allLayers, grid, output);
        }
    }
}
