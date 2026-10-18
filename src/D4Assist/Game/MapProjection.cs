using D4Assist.Navigation;
using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Where a world point shows on the open world map, in the game window's client pixels (research/client-memory.md §4, world
/// map view: three screenshots at 1920×1080, waypoints placed within about 3 px). The map uses the play camera's axes
/// (<see cref="ScreenDirections"/>) and a scale of <see cref="ScaleAtReference"/> / zoom pixels per unit at a 1080-px-high client.
/// </summary>
public static class MapProjection
{
    private const double ReferenceHeight = 1080;

    /// <summary>Pixels per world unit along a screen axis, times the zoom, at a 1080-px-high client (≈ ⅔ of the height).</summary>
    public const double ScaleAtReference = 732;

    // The map area at 1080 px: the whole width but the side bar on the right, below the tab bar, above the button hints.
    private const double SideBarWidth = 80, AreaTop = 70, AreaBottom = 1015;

    /// <summary>The client pixel of world point (<paramref name="worldX"/>, <paramref name="worldY"/>); null when it falls outside
    /// the map area (zoom out or pan to bring it in).</summary>
    public static (int X, int Y)? ToClient(MapView view, double worldX, double worldY, int clientWidth, int clientHeight)
    {
        if (clientWidth <= 0 || clientHeight <= 0) return null;
        double unit = clientHeight / ReferenceHeight;
        double pixelsPerAxisUnit = ScaleAtReference * unit / view.Zoom * Math.Sqrt(2); // ToScreen scales the diagonals by 1/√2
        var (right, up) = ScreenDirections.ToScreen(worldX - view.CenterX, worldY - view.CenterY);
        double areaRight = clientWidth - SideBarWidth * unit;
        double x = areaRight / 2 + right * pixelsPerAxisUnit, y = clientHeight / 2.0 - up * pixelsPerAxisUnit;
        if (x < 0 || x >= areaRight || y < AreaTop * unit || y >= AreaBottom * unit) return null;
        return ((int)Math.Round(x), (int)Math.Round(y));
    }
}
