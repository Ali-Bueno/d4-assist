namespace D4Memory.SceneRoute;

/// <summary>
/// The build-specific RVAs/offsets the static scene route (research/client-memory.md §1) needs, keyed by
/// the exe's PE TimeDateStamp so a route derived for one build is never applied to another.
/// </summary>
/// <param name="GameObjectPointerRva">RVA of the u64 pointer to the game object G.</param>
/// <param name="SceneContainerOffset">Offset of the u64 pointer to the scene container P, inside G.</param>
/// <param name="SeedRva">RVA of the u32 per-session seed used to pick the chunk-table index k.</param>
/// <param name="CapacityOffset">Offset of the container's u32 capacity field, inside P.</param>
/// <param name="MaxIndexOffset">Offset of the container's u32 inclusive max-index field, inside P.</param>
/// <param name="ShiftOffset">Offset of the container's u32 chunk-index shift, inside P.</param>
/// <param name="MaskOffset">Offset of the container's u32 in-chunk index mask, inside P.</param>
/// <param name="ChunkTableOffset">Offset of the first of 16 chunk-selector entries, inside P.</param>
/// <param name="CSceneVtableRva">RVA of the CScene vtable a valid slot's record must point at.</param>
public sealed record GameBuildOffsets(
    long GameObjectPointerRva,
    long SceneContainerOffset,
    long SceneContainerSize,
    long SeedRva,
    long CapacityOffset,
    long MaxIndexOffset,
    long ShiftOffset,
    long MaskOffset,
    long ChunkTableOffset,
    long CSceneVtableRva);

/// <summary>Looks up <see cref="GameBuildOffsets"/> by the installed exe's PE TimeDateStamp (see PeHeaderReader).</summary>
public static class GameBuildOffsetsTable
{
    // PE TimeDateStamp of "Diablo IV.exe" for build 3.2.1.73552 (read offline from the on-disk exe header,
    // the same field ModuleLocator already matches — no process access). Every offset below for this key
    // comes from research/client-memory.md §1 ("Static route to the pool").
    private const uint Build_3_2_1_73552_TimeDateStamp = 0x6A973496;

    private static readonly Dictionary<uint, GameBuildOffsets> ByTimeDateStamp = new()
    {
        [Build_3_2_1_73552_TimeDateStamp] = new GameBuildOffsets(
            GameObjectPointerRva: 0x368D188,
            SceneContainerOffset: 0xC18,
            SceneContainerSize: 0x2B8,
            SeedRva: 0x37CC63C,
            CapacityOffset: 0x100,
            MaxIndexOffset: 0x108,
            ShiftOffset: 0x2B0,
            MaskOffset: 0x2B4,
            ChunkTableOffset: 0x120,
            CSceneVtableRva: 0x2819030),
    };

    /// <summary>False when the build isn't known — callers must fall back to the full signature scan.</summary>
    public static bool TryGet(uint timeDateStamp, out GameBuildOffsets offsets) =>
        ByTimeDateStamp.TryGetValue(timeDateStamp, out offsets!);
}
