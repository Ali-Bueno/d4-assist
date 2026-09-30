using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>Where the module image lies; a screen record's +0x58 pointer must fall inside it.</summary>
public readonly record struct ModuleRange(long Base, long Size)
{
    public bool Contains(long address) => address >= Base && address < Base + Size;
}

/// <summary>Names of the game menus that mute the cues (research/client-memory.md §5); build-independent.</summary>
public static class MenuScreens
{
    /// <summary>Character panel (inventory, skills).</summary>
    public const string CharacterPanel = "screen_MenuPanel_Default";

    /// <summary>Map and pause menu.</summary>
    public const string MainMenu = "screen_MainInGameMenu";

    /// <summary>A readable sign's text (warning signs, notice boards; live 2026-09-30).</summary>
    public const string Sign = "screen_Signs";

    /// <summary>Horadric shipments, the mailbox of unclaimed items (live 2026-09-30).</summary>
    public const string Mailbox = "screen_Mailbox_Default";

    /// <summary>Vendor (buy and sell; live 2026-09-30).</summary>
    public const string Vendor = "screen_Shop_default";

    /// <summary>Artisans: one generic screen (live 2026-09-30 with the blacksmith and the occultist).</summary>
    public const string Crafting = "screen_crafting_default";

    /// <summary>Stash (live 2026-09-30).</summary>
    public const string Stash = "screen_Stash_default";

    public static readonly IReadOnlyList<string> Watched = [CharacterPanel, MainMenu, Sign, Mailbox, Vendor, Crafting, Stash];
}

/// <summary>Finds the heap screen registry by scan and reads each screen's open flag (research/client-memory.md §5).</summary>
public static class ScreenRegistryReader
{
    // Registry entry {u64 next, u32 key, u32 padding, u64 value}: the key is the name hash, the value points to the screen
    // record. The padding holds leftover heap bytes (zero in one session, 0x258 in the next), so only the u32 is matched.
    private const int KeyOffsetInEntry = 8, ValueOffsetInEntry = 16, EntryAlignment = 8;

    // Screen record: u32 flags (bit 0 = open) at +0x10; a u64 at +0x58 points into the module image on a real record.
    private const int OpenFlagOffset = 0x10, ModulePointerOffset = 0x58;
    private const uint OpenBit = 1;

    // Name hash: h = h * 33 + c over the lowercased ASCII name (u32).
    private const uint HashMultiplier = 33;

    // The registry sits in private read-write heap memory.
    private const uint HeapProtection = NativeMethods.PageReadWrite, HeapType = NativeMethods.MemPrivate;

    // Half the cores, leaving the rest to the game. Live 2026-09-30 (16 cores, 14 GB heap): a full-heap miss took 8.7 s
    // instead of 57 s sequentially, a hit 0.3 s instead of 3.3 s.
    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    private static readonly int RecordReadBytes = ModulePointerOffset + sizeof(long);

    public static uint Hash(string name)
    {
        uint h = 0;
        foreach (char c in name) h = unchecked(h * HashMultiplier + char.ToLowerInvariant(c));
        return h;
    }

    /// <summary>Scans heap regions in parallel and returns name to record address, stopping once every name is found.</summary>
    /// <remarks>Each name has one record that validates (research/client-memory.md §5), so the scan order doesn't matter.</remarks>
    public static Dictionary<string, long> Locate(IMemoryReader memory, IReadOnlyCollection<string> names, ModuleRange module)
    {
        var found = new ConcurrentDictionary<string, long>();
        var keys = names.ToDictionary(Hash);
        var regions = memory.Regions().Where(r => r.Type == HeapType && r.Protection == HeapProtection);
        var options = new ParallelOptions { MaxDegreeOfParallelism = ScanThreads };
        Parallel.ForEach(regions, options, () => new byte[RegionWindows.DefaultWindowBytes], (region, loop, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                if (loop.IsStopped) break;
                FindInWindow(memory, module, keys, found, address, data.Span);
                if (found.Count == keys.Count) { loop.Stop(); break; }
            }
            return buffer;
        }, _ => { });
        return new Dictionary<string, long>(found);
    }

    private static void FindInWindow(
        IMemoryReader memory, ModuleRange module, Dictionary<uint, string> keys, ConcurrentDictionary<string, long> found,
        long address, ReadOnlySpan<byte> data)
    {
        var words = MemoryMarshal.Cast<byte, uint>(data[..(data.Length & ~(sizeof(uint) - 1))]);
        var word = new byte[sizeof(long)];
        foreach (var (key, name) in keys)
        {
            if (found.ContainsKey(name)) continue;
            for (int from = 0, hit; from < words.Length && (hit = words[from..].IndexOf(key)) >= 0; from = from + hit + 1)
            {
                long keyAddress = address + (long)(from + hit) * sizeof(uint);
                if (keyAddress % EntryAlignment != 0) continue;
                long entry = keyAddress - KeyOffsetInEntry;
                if (!memory.TryRead(entry + ValueOffsetInEntry, word, sizeof(long))) continue;
                long record = BinaryPrimitives.ReadInt64LittleEndian(word);
                if (TryReadOpen(memory, record, module) is null) continue;
                found.TryAdd(name, record);
                break;
            }
        }
    }

    /// <summary>Open (true) or closed (false); null when the record is unreadable or no longer looks like a screen record.</summary>
    public static bool? TryReadOpen(IMemoryReader memory, long record, ModuleRange module)
    {
        var bytes = new byte[RecordReadBytes];
        if (record <= 0 || !memory.TryRead(record, bytes, bytes.Length)) return null;
        if (!module.Contains(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(ModulePointerOffset)))) return null;
        return (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(OpenFlagOffset)) & OpenBit) != 0;
    }
}
