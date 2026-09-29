using D4Memory.Ui;

namespace D4Memory.SceneRoute;

/// <summary>Layout shared by every pool container (0x2B8-byte template, research/client-memory.md §1, §4).</summary>
/// <param name="ContainerSize">Size of the container object; k is derived from (address / size).</param>
/// <param name="CapacityOffset">Offset of the u32 capacity field, inside the container.</param>
/// <param name="MaxIndexOffset">Offset of the u32 inclusive max-index field.</param>
/// <param name="ShiftOffset">Offset of the u32 chunk-index shift.</param>
/// <param name="MaskOffset">Offset of the u32 in-chunk index mask.</param>
/// <param name="ChunkTableOffset">Offset of the first of 16 chunk-selector entries.</param>
public sealed record PoolLayout(
    long ContainerSize,
    long CapacityOffset,
    long MaxIndexOffset,
    long ShiftOffset,
    long MaskOffset,
    long ChunkTableOffset);

/// <summary>Per-container parameters of one pool.</summary>
/// <param name="ContainerOffset">Offset of the u64 container pointer inside the game object G.</param>
/// <param name="SeedRva">RVA of the u32 per-session seed used to pick the chunk-table index k.</param>
/// <param name="StrideBase">Base of the seeded stride formula.</param>
public sealed record PoolParameters(long ContainerOffset, long SeedRva, int StrideBase);

/// <summary>
/// The build-specific RVAs/offsets the static routes (research/client-memory.md §1, §4) need, keyed by the
/// exe's PE TimeDateStamp so a route derived for one build is never applied to another.
/// </summary>
/// <param name="GameObjectPointerRva">RVA of the u64 pointer to the game object G.</param>
/// <param name="CSceneVtableRva">RVA of the CScene vtable a valid scene slot's record must point at.</param>
/// <param name="Conversation">Dialogue menu and playing-conversation addresses; null when unknown for the build.</param>
public sealed record GameBuildOffsets(
    long GameObjectPointerRva,
    PoolLayout Pool,
    PoolParameters Scene,
    PoolParameters Actor,
    long CSceneVtableRva,
    ConversationOffsets? Conversation = null);

/// <summary>Looks up <see cref="GameBuildOffsets"/> by the installed exe's PE TimeDateStamp (see PeHeaderReader).</summary>
public static class GameBuildOffsetsTable
{
    // PE TimeDateStamp of "Diablo IV.exe" for build 3.2.1.73552 (read offline from the on-disk exe header,
    // the same field ModuleLocator already matches — no process access). Every offset below for this key
    // comes from research/client-memory.md §1 (scene) and §4 (RActor; offline RE, not yet validated live).
    private const uint Build_3_2_1_73552_TimeDateStamp = 0x6A973496;

    private static readonly Dictionary<uint, GameBuildOffsets> ByTimeDateStamp = new()
    {
        [Build_3_2_1_73552_TimeDateStamp] = new GameBuildOffsets(
            GameObjectPointerRva: 0x368D188,
            Pool: new PoolLayout(
                ContainerSize: 0x2B8,
                CapacityOffset: 0x100,
                MaxIndexOffset: 0x108,
                ShiftOffset: 0x2B0,
                MaskOffset: 0x2B4,
                ChunkTableOffset: 0x120),
            Scene: new PoolParameters(ContainerOffset: 0xC18, SeedRva: 0x37CC63C, StrideBase: 0x92F),
            Actor: new PoolParameters(ContainerOffset: 0xB78, SeedRva: 0x37CC5A0, StrideBase: 0xC27),
            CSceneVtableRva: 0x2819030,
            // Live pointer snapshots while talking to Vani, 2026-09-29 (client-memory.md §5).
            Conversation: new ConversationOffsets(
                OptionsPointerRva: 0x31326A0, OptionStride: 0x20, OptionSnoOffset: 0x10, PlayingListRva: 0x30D28F8, NodeSnoOffset: 0x28)),
    };

    /// <summary>False when the build isn't known — callers must fall back to the full signature scan.</summary>
    public static bool TryGet(uint timeDateStamp, out GameBuildOffsets offsets) =>
        ByTimeDateStamp.TryGetValue(timeDateStamp, out offsets!);
}
