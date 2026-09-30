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

/// <summary>Names of the game screens that mute the cues (research/client-memory.md §5); build-independent.</summary>
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

    // The other side panels, not yet opened live. The game lays out each panel as a "<name>_Default" screen plus Left/Right
    // variants; this is every such screen in the registry (292 screens, 2026-09-30) except the HUD action bar, plus
    // WarPlans (no _Default variant). `--debug` names each screen as it opens.
    private static readonly string[] OtherPanels =
    [
        "screen_Abilities_Default", "screen_Armory_Default", "screen_Board_Select_Default", "screen_deathmenu_default",
        "screen_deathmenu_hardcore_default", "screen_DruidSkills_Default", "screen_DruidSpirit_Panel_Default",
        "screen_Glyph_Upgrade_V2_Default", "screen_Inventory_default", "screen_Mercenary_Default",
        "screen_NecromancerMechanic_Default", "screen_PaladinMechanic_Default", "screen_PlayerProfile2_Default",
        "screen_PlayerProfile2_InGame_Default", "screen_PlayerProfile_Abilities_Default",
        "screen_PlayerProfile_Abilities_InGame_Default", "screen_PlayerProfile_Accomplishments_Default",
        "screen_PlayerProfile_Accomplishments_InGame_Default", "screen_PlayerProfile_Clan_Default",
        "screen_PlayerProfile_Clan_InGame_Default", "screen_PlayerProfile_Overview_Default",
        "screen_PlayerProfile_Overview_InGame_Default", "screen_Quest_MultiReward_default", "screen_RogueSpecializations_Default",
        "screen_RulerRealm_Select_Default", "screen_Seasonal_ReputationDefault", "screen_seasonalinv_default",
        "screen_SocialWheel_Customization_Default", "screen_SpiritbornMechanic_Default", "screen_Summon_Boss_default",
        "screen_Trading_Default", "screen_Trading_Local_Default", "screen_WarlockMechanic_Default",
        "screen_WarPlanActivities_Default", "screen_WarPlans", "screen_WeaponExpertise_Default",
    ];

    // Full screens the player opens on purpose, not yet opened live either. Left out: screens seen open during normal play
    // (Chat, SocialNotifications, ConversationSubtitle, GenericSkillTreeTest, StoreDetails_v2, Mailbox_Right, InGameLogout,
    // WorldTiers, FollowerSwap, ModalRenameLoadout, 2026-09-30), and HUD, tooltips, tutorials, popups, front end and debug.
    private static readonly string[] OtherMenus =
    [
        "screen_Achievements", "screen_BrightnessDialog", "screen_BugReport", "screen_BuildViewer", "screen_CampaignProgress",
        "screen_ChallengeRift", "screen_ChallengeRiftLeaderboards", "screen_Clan", "screen_ClanProfile",
        "screen_ClanProfileWithApplication", "screen_ClanProfileWithInvite", "screen_ClanProfileWithPendingApplication",
        "screen_ClanSettingsMember", "screen_ClanSettingsOfficer", "screen_CodexOfPower", "screen_Collections",
        "screen_ContentReport", "screen_Credits", "screen_Customization", "screen_GameGuide", "screen_GameOptions",
        "screen_HallOfFame", "screen_Hall_Of_Fallen_Heroes", "screen_Hireling", "screen_HirelingDetailsContainer",
        "screen_Inspect", "screen_LootFilter", "screen_Mail", "screen_PartyFinder", "screen_PlayerChoiceUI",
        "screen_PlayerReport", "screen_RaidDifficulty", "screen_Reliquaries", "screen_Reliquary", "screen_SeasonObjectives_V2",
        "screen_SeasonUpgrades", "screen_SocialScreen", "screen_StableMaster", "screen_Store", "screen_StoreDetails",
        "screen_Store_AddOnDetails", "screen_Wardrobe",
    ];

    public static readonly IReadOnlyList<string> Watched =
        [CharacterPanel, MainMenu, Sign, Mailbox, Vendor, Crafting, Stash, .. OtherPanels, .. OtherMenus];
}

/// <summary>Finds the heap screen registry by scan and reads each screen's open flag (research/client-memory.md §5).</summary>
public static class ScreenRegistryReader
{
    // Registry entry {u64 next, u32 key, u32 padding, u64 value}: the key is the name hash, the value points to the screen
    // record. The padding holds leftover heap bytes (zero in one session, 0x258 in the next), so only the u32 is matched.
    private const int KeyOffsetInEntry = 8, ValueOffsetInEntry = 16, EntryAlignment = 8;

    // Screen record: a bool at +0x10 (open; the next bytes belong to other fields, only this byte toggles) and a u64 at
    // +0x58 that points into the module image on a real record.
    private const int OpenFlagOffset = 0x10, ModulePointerOffset = 0x58;

    // Name hash: h = h * 33 + c over the lowercased ASCII name (u32).
    private const uint HashMultiplier = 33;

    // The registry sits in private read-write heap memory.
    private const uint HeapProtection = NativeMethods.PageReadWrite, HeapType = NativeMethods.MemPrivate;

    // Half the cores, leaving the rest to the game. Live 2026-09-30 (16 cores, 15 GB heap): all 43 watched screens found
    // in 0.5 s; a full-heap miss took 9.7 s (57 s sequentially).
    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    private static readonly int RecordReadBytes = ModulePointerOffset + sizeof(long);

    // A scan looks for tens of keys: a table indexed by a key's low 16 bits rejects almost every heap word before the
    // dictionary lookup.
    private const int FilterBits = 16;
    private const uint FilterMask = (1u << FilterBits) - 1;

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
        var filter = new bool[1 << FilterBits];
        foreach (uint key in keys.Keys) filter[key & FilterMask] = true;
        var regions = memory.Regions().Where(r => r.Type == HeapType && r.Protection == HeapProtection);
        var options = new ParallelOptions { MaxDegreeOfParallelism = ScanThreads };
        Parallel.ForEach(regions, options, () => new byte[RegionWindows.DefaultWindowBytes], (region, loop, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                if (loop.IsStopped) break;
                FindInWindow(memory, module, keys, filter, found, address, data.Span);
                if (found.Count == keys.Count) { loop.Stop(); break; }
            }
            return buffer;
        }, _ => { });
        return new Dictionary<string, long>(found);
    }

    private static void FindInWindow(
        IMemoryReader memory, ModuleRange module, Dictionary<uint, string> keys, bool[] filter,
        ConcurrentDictionary<string, long> found, long address, ReadOnlySpan<byte> data)
    {
        int first = (int)((EntryAlignment - address % EntryAlignment) % EntryAlignment);
        if (first >= data.Length) return;
        var slots = MemoryMarshal.Cast<byte, ulong>(data[first..][..((data.Length - first) & ~(EntryAlignment - 1))]);
        var word = new byte[sizeof(long)];
        for (int i = 0; i < slots.Length; i++)
        {
            uint key = (uint)slots[i]; // entries are 8-aligned, so a key is the low (little-endian) half of an aligned slot
            if (!filter[key & FilterMask] || !keys.TryGetValue(key, out var name) || found.ContainsKey(name)) continue;
            long entry = address + first + (long)i * EntryAlignment - KeyOffsetInEntry;
            if (!memory.TryRead(entry + ValueOffsetInEntry, word, sizeof(long))) continue;
            long record = BinaryPrimitives.ReadInt64LittleEndian(word);
            if (TryReadOpen(memory, record, module) is not null) found.TryAdd(name, record);
        }
    }

    /// <summary>Open (true) or closed (false); null when the record is unreadable or no longer looks like a screen record.</summary>
    public static bool? TryReadOpen(IMemoryReader memory, long record, ModuleRange module)
    {
        var bytes = new byte[RecordReadBytes];
        if (record <= 0 || !memory.TryRead(record, bytes, bytes.Length)) return null;
        if (!module.Contains(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(ModulePointerOffset)))) return null;
        return bytes[OpenFlagOffset] switch { 0 => false, 1 => true, _ => null }; // other values: freed memory (character switch)
    }
}
