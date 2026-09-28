using D4Data.Model;

namespace D4MemScan.Scan;

/// <summary>One distinct CScene tile hit, as written to the report.</summary>
public sealed record TileHitRecord(
    string Address, int Sno, string? Name, float[] Wp, float[] Q,
    int ExitMask, uint Hash, bool HashMatchesFormula, string Vtable);

public sealed record VtableCount(string Vtable, int Count);

/// <summary>One revealed-scene list entry (no position; see RevealedListMatcher).</summary>
public sealed record RevealedRecord(uint IdSScene, int Sno, string? Name, uint K);

public sealed record SceneScanReport(
    string Tool, SnoRef? World,
    IReadOnlyList<TileHitRecord> Tiles,
    IReadOnlyList<VtableCount> Vtables,
    Box Bounds,
    ExitConsistencyResult ExitConsistency,
    IReadOnlyList<RevealedRecord> Revealed,
    IReadOnlyList<string> AsciiMap,
    IReadOnlyList<string> Notes);
