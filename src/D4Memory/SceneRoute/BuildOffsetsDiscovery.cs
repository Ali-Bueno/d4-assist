using System.Buffers.Binary;
using System.Text;
using D4Memory.Pools;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4Memory.SceneRoute;

/// <summary>
/// Finds the build-specific addresses in the live process by what they hold, so a game update needs no new constants
/// (research/client-memory.md §6). The structure offsets come from <see cref="GameBuildOffsetsTable.Layout"/>; only the
/// addresses are searched:
/// - The game object G: a pointer in the module's data whose G+0xAB0 leads to the container named "ActorCommonData" (§4).
/// - The attribute name table: the descriptor that points to the "Gizmo_Has_Been_Operated" string (§4).
/// The pools' per-session seeds are not needed (PoolContainer validates the chunk table instead). Conversations are not
/// found here yet: <see cref="GameBuildOffsets.Conversation"/> stays null.
/// </summary>
public static class BuildOffsetsDiscovery
{
    private static readonly byte[] AcdContainerName = Encoding.ASCII.GetBytes("ActorCommonData\0");
    private static readonly byte[] KnownAttributeName = Encoding.ASCII.GetBytes("Gizmo_Has_Been_Operated\0");

    // Heap pointers on x64 Windows user space; anything else in the module's data is not a pointer to G.
    private const ulong MinPointer = 0x10000, MaxPointer = 0x7FFF_FFFF_0000;

    /// <summary>The build's addresses, or null (with the reason in <paramref name="report"/>) when G is not found.</summary>
    public static GameBuildOffsets? Discover(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> imageRegions, ModuleRange module, out string report)
    {
        var layout = GameBuildOffsetsTable.Layout;
        var data = ModuleLocator.WritableRegionsAt(imageRegions, module.Base);
        var pointers = DataPointers(memory, data, module);

        long? gWord = FindGameObjectPointer(memory, pointers, layout, out string misses);
        if (gWord is not { } g)
        {
            report = $"game object not found ({pointers.Count} data pointers checked; {misses})";
            return null;
        }

        long? table = layout.Acd is { } acd ? FindAttributeTable(memory, imageRegions, module, acd) : null;
        report = $"game object at RVA 0x{g - module.Base:X}, attribute names " +
                 (table is { } t ? $"at RVA 0x{t - module.Base:X}" : "not found");
        return layout with
        {
            GameObjectPointerRva = g - module.Base,
            Acd = table is { } found && layout.Acd is { } a ? a with { AttributeTableRva = found - module.Base } : null,
        };
    }

    /// <summary>Every heap-looking u64 in the module's writable data, by value, with the lowest address holding it.</summary>
    private static Dictionary<ulong, long> DataPointers(IMemoryReader memory, IReadOnlyList<MemoryRegion> data, ModuleRange module)
    {
        var pointers = new Dictionary<ulong, long>();
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in data)
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                var span = window.Span;
                for (int i = (int)((8 - address % 8) % 8); i + 8 <= span.Length; i += 8)
                {
                    ulong value = BinaryPrimitives.ReadUInt64LittleEndian(span[i..]);
                    if (value is < MinPointer or > MaxPointer || module.Contains((long)value)) continue;
                    long at = address + i;
                    if (!pointers.TryGetValue(value, out long first) || at < first) pointers[value] = at;
                }
            }
        return pointers;
    }

    // G+0xAB0 holds a pointer to the ACD container pointer; the container starts with its name (§4). The scene and actor
    // container pointers must be there too. <paramref name="misses"/> counts the near misses, for the log.
    private static long? FindGameObjectPointer(
        IMemoryReader memory, Dictionary<ulong, long> pointers, GameBuildOffsets layout, out string misses)
    {
        misses = "no ACD layout";
        if (layout.Acd is not { } acd) return null;
        var name = new byte[AcdContainerName.Length];
        long? best = null;
        int named = 0, noScene = 0, noActor = 0;
        foreach (var (g, at) in pointers)
        {
            if (!PoolContainer.TryReadU64(memory, (long)g + acd.Pool.ContainerOffset, out ulong cell) || cell == 0) continue;
            if (!PoolContainer.TryReadU64(memory, (long)cell, out ulong container) || container == 0) continue;
            if (!memory.TryRead((long)container, name, name.Length) || !name.AsSpan().SequenceEqual(AcdContainerName)) continue;
            named++;
            if (!PoolContainer.TryReadU64(memory, (long)g + layout.Scene.ContainerOffset, out ulong scene) || scene == 0) { noScene++; continue; }
            if (!PoolContainer.TryReadU64(memory, (long)g + layout.Actor.ContainerOffset, out ulong actor) || actor == 0) { noActor++; continue; }
            if (best is null || at < best) best = at;
        }
        misses = $"{named} lead to ActorCommonData, {noScene} of them without a scene container, {noActor} without an actor container";
        return best;
    }

    // The descriptor array: entry i is attribute id i (u32 at +0), with a pointer to its name at AttributeNameOffset.
    private static long? FindAttributeTable(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> imageRegions, ModuleRange module, AcdOffsets acd)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in imageRegions.Where(r => r.AllocationBase == module.Base))
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                var span = window.Span;
                for (int from = 0, hit; (hit = span[from..].IndexOf(KnownAttributeName)) >= 0; from += hit + 1)
                {
                    int at = from + hit;
                    if (at > 0 && span[at - 1] != 0) continue; // a longer name that ends with this one
                    if (FindPointerTo(memory, imageRegions, module, address + at) is not { } nameField) continue;
                    long descriptor = nameField - acd.AttributeNameOffset;
                    if (!PoolContainer.TryReadU32(memory, descriptor, out uint id)) continue;
                    long table = descriptor - (long)id * acd.AttributeStride;
                    if (IsDescriptorTable(memory, table, acd)) return table;
                }
            }
        return null;
    }

    private static long? FindPointerTo(IMemoryReader memory, IReadOnlyList<MemoryRegion> imageRegions, ModuleRange module, long target)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in ModuleLocator.WritableRegionsAt(imageRegions, module.Base))
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                var span = window.Span;
                for (int i = (int)((8 - address % 8) % 8); i + 8 <= span.Length; i += 8)
                    if (BinaryPrimitives.ReadInt64LittleEndian(span[i..]) == target) return address + i;
            }
        return null;
    }

    private static bool IsDescriptorTable(IMemoryReader memory, long table, AcdOffsets acd)
    {
        for (uint id = 0; id < 3; id++)
            if (!PoolContainer.TryReadU32(memory, table + id * acd.AttributeStride, out uint stored) || stored != id) return false;
        return true;
    }
}
