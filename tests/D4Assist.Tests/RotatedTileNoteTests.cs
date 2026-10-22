using D4Assist.Game;

namespace D4Assist.Tests;

public class RotatedTileNoteTests
{
    private static TileState Tile(float yaw) => new(42, "DGN_Test_Corridor_EW", 64, 32, 32, 32, 0, 1, 1, yaw);

    [Theory]
    [InlineData(0f)]
    [InlineData(0.4f)] // float noise around an identity quaternion
    [InlineData(360f)]
    [InlineData(-0.4f)]
    public void An_unrotated_tile_says_nothing(float yaw) => Assert.Null(NavigationBridge.RotatedTileNote(Tile(yaw)));

    [Theory]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(-90f)]
    [InlineData(45f)] // off the quarter turns too
    public void A_rotated_tile_is_named_with_its_yaw(float yaw)
    {
        var note = NavigationBridge.RotatedTileNote(Tile(yaw));

        Assert.NotNull(note);
        Assert.Contains("DGN_Test_Corridor_EW", note);
        Assert.Contains($"yaw={yaw:F1}", note);
    }
}
