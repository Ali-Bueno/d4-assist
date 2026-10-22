using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>What the world map shows: its center in world units and its zoom (larger is farther out).</summary>
public readonly record struct MapView(float CenterX, float CenterY, float Zoom);

/// <summary>Reads the world map's view (research/client-memory.md §4, world map view); meaningful while screen_Map is open.</summary>
public static class MapViewReader
{
    // [G + 0xC78] → the map object: f32 zoom at +0x2F4, f32 center X, Y at +0x2FC (live 2026-10-18, build 3.2.3.73883).
    private const int MapObjectOffset = 0xC78, ZoomOffset = 0x2F4, CenterOffset = 0x2FC;

    // Zoom steps seen live: 125.6, 417.6, 1001.6, 1877.6. Anything far outside is not the map object.
    private const float MinZoom = 10, MaxZoom = 100_000, MaxCoordinate = 1_000_000;

    /// <summary>Null when the map object is unreadable or its values are no map view.</summary>
    public static MapView? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + MapObjectOffset, out ulong map) || map == 0) return null;
        var bytes = new byte[CenterOffset + 2 * sizeof(float) - ZoomOffset];
        if (!memory.TryRead((long)map + ZoomOffset, bytes, bytes.Length)) return null;
        float zoom = BitConverter.ToSingle(bytes, 0);
        float x = BitConverter.ToSingle(bytes, CenterOffset - ZoomOffset), y = BitConverter.ToSingle(bytes, CenterOffset - ZoomOffset + sizeof(float));
        if (!float.IsFinite(zoom) || zoom is < MinZoom or > MaxZoom) return null;
        if (!float.IsFinite(x) || !float.IsFinite(y) || Math.Abs(x) > MaxCoordinate || Math.Abs(y) > MaxCoordinate) return null;
        return new MapView(x, y, zoom);
    }
}
