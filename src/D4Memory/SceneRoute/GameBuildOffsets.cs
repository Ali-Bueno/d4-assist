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
/// <param name="StrideBase">Base of the seeded stride formula.</param>
/// <param name="ContainerBehindPointer">True when G+ContainerOffset holds a pointer to the container pointer (the ACD pool).</param>
public sealed record PoolParameters(long ContainerOffset, int StrideBase, bool ContainerBehindPointer = false);

/// <summary>ACD pool and attribute descriptor table (research/client-memory.md §4, live 2026-09-30).</summary>
/// <param name="AttributeTableRva">RVA of the attribute descriptor array; entry i describes attribute id i.</param>
/// <param name="AttributeStride">Bytes per descriptor.</param>
/// <param name="AttributeNameOffset">Offset of a descriptor's pointer to its ASCII name.</param>
public sealed record AcdOffsets(PoolParameters Pool, long AttributeTableRva, int AttributeStride, int AttributeNameOffset);

/// <summary>
/// The build-specific RVAs/offsets the static routes (research/client-memory.md §1, §4) need, keyed by the
/// exe's PE TimeDateStamp so a route derived for one build is never applied to another.
/// </summary>
/// <param name="GameObjectPointerRva">RVA of the u64 pointer to the game object G.</param>
/// <param name="CSceneVtableRva">RVA of the CScene vtable a valid scene slot's record must point at; 0 skips the check.</param>
/// <param name="Conversation">Dialogue menu and playing-conversation addresses; null when unknown for the build.</param>
/// <param name="Acd">ACD pool and attribute names; null when unknown for the build.</param>
public sealed record GameBuildOffsets(
    long GameObjectPointerRva,
    PoolLayout Pool,
    PoolParameters Scene,
    PoolParameters Actor,
    long CSceneVtableRva,
    ConversationOffsets? Conversation = null,
    AcdOffsets? Acd = null);

/// <summary>The structure layout the game's memory is read with; the addresses in it are found per build.</summary>
public static class GameBuildOffsetsTable
{
    /// <summary>
    /// Offsets inside the game's structures, from build 3.2.1.73552 (research/client-memory.md §1, §4, §5). They survive
    /// small updates; the addresses (RVAs) don't, so they are 0 here and <see cref="BuildOffsetsDiscovery"/> fills them
    /// from the live process. Build 3.2.1.73552's RVAs are kept in research/client-memory.md.
    /// </summary>
    public static readonly GameBuildOffsets Layout = new(
        GameObjectPointerRva: 0,
        Pool: new PoolLayout(
            ContainerSize: 0x2B8,
            CapacityOffset: 0x100,
            MaxIndexOffset: 0x108,
            ShiftOffset: 0x2B0,
            MaskOffset: 0x2B4,
            ChunkTableOffset: 0x120),
        Scene: new PoolParameters(ContainerOffset: 0xC18, StrideBase: 0x92F),
        Actor: new PoolParameters(ContainerOffset: 0xB78, StrideBase: 0xC27),
        CSceneVtableRva: 0,
        Acd: new AcdOffsets(
            Pool: new PoolParameters(ContainerOffset: 0xAB0, StrideBase: 0x627, ContainerBehindPointer: true),
            AttributeTableRva: 0, AttributeStride: 0x58, AttributeNameOffset: 0x40));

    /// <summary>The conversation records' layout (client-memory.md §5); the addresses come from ConversationDiscovery.</summary>
    public static readonly ConversationOffsets ConversationLayout =
        new(OptionsPointerRva: 0, OptionStride: 0x20, OptionSnoOffset: 0x10, PlayingListRva: 0, NodeSnoOffset: 0x28);
}
