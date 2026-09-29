using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4MemScan.Commands;

/// <summary>Cross-checks the RActor pool against the player's static record (research/client-memory.md §4).</summary>
internal static class ActorsValidation
{
    /// <summary>Max distance (world units) between the static record's position and its RActor to call them the same actor.</summary>
    private const float PlayerMatchToleranceUnits = 1f;

    /// <summary>Offset of the u32 printed next to the world id, to learn if world id is an SNO or an instance id.</summary>
    private const int SceneSlotWorldProbeOffset = 0x18;

    /// <summary>Prints PASS/FAIL per check and returns the RActor matched to the player, or null.</summary>
    public static RActorRecord? Run(
        IMemoryReader memory, long moduleBase, GameBuildOffsets offsets, IReadOnlyList<RActorRecord> actors, PlayerRecordHit player)
    {
        var matches = actors.Where(a => a.IsPlaced && a.ActorSno == player.ActorSno
            && System.Numerics.Vector3.Distance(a.Position, player.Position) < PlayerMatchToleranceUnits).ToList();
        Console.WriteLine($"[{Verdict(matches.Count == 1)}] (a) exactly one placed RActor with player SNO {player.ActorSno} within " +
                          $"{PlayerMatchToleranceUnits} unit of the record: {matches.Count} found");
        if (matches.Count != 1) return matches.Count > 0 ? matches[0] : null;

        var actor = matches[0];
        bool scenePoolOk = PoolContainer.TryOpen(memory, moduleBase, offsets, offsets.Scene, out var scenePool);
        long sceneSlot = 0;
        bool found = scenePoolOk && scenePool.TryGetSlotById(actor.SceneId, out sceneSlot);

        int sceneSno = 0;
        bool read = found && PoolContainer.TryReadU32(memory, sceneSlot + SceneRouteReader.SnoSceneOffsetInSlot, out uint raw)
            && (sceneSno = (int)raw) == player.SceneSno;
        Console.WriteLine($"[{Verdict(read)}] (b) scene id 0x{actor.SceneId:X} -> scene slot {(found ? $"0x{sceneSlot:X}" : "not found")}, " +
                          $"snoScene {(found ? sceneSno.ToString() : "?")} vs record {player.SceneSno}");

        string probe = found && PoolContainer.TryReadU32(memory, sceneSlot + SceneSlotWorldProbeOffset, out uint p) ? $"0x{p:X}" : "?";
        Console.WriteLine($"(c) actor world id 0x{actor.WorldId:X} ({actor.WorldId}); record p-12 world SNO {player.WorldSno}; " +
                          $"scene slot u32@+0x{SceneSlotWorldProbeOffset:X} = {probe}");
        return actor;
    }

    private static string Verdict(bool ok) => ok ? "PASS" : "FAIL";
}
