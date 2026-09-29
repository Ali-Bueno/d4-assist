namespace D4Assist.Input;

public enum SonyPadModel { DualShock4, DualSense }

/// <summary>Decodes the D-pad from DualShock 4 / DualSense HID input reports (layouts as in Linux hid-playstation and SDL).</summary>
public static class SonyPadReport
{
    public const ushort SonyVendorId = 0x054C;

    // Low nibble of the first button byte: 0 = up, clockwise to 7 = up-left, 8 = released.
    private const int HatMask = 0x0F;
    private const int HatDownLeft = 5, HatLeft = 6, HatUpLeft = 7;

    /// <summary>The model for a HID vendor/product id pair; null for anything else.</summary>
    public static SonyPadModel? ModelOf(ushort vendorId, ushort productId) => vendorId != SonyVendorId ? null : productId switch
    {
        0x05C4 or 0x09CC or 0x0BA0 => SonyPadModel.DualShock4, // v1, v2, USB wireless adapter
        0x0CE6 or 0x0DF2 => SonyPadModel.DualSense,            // DualSense, DualSense Edge
        _ => null,
    };

    /// <summary>True while the D-pad points left, up-left or down-left (like XInput's DPAD_LEFT); null for a report without buttons.</summary>
    public static bool? IsDpadLeft(ReadOnlySpan<byte> report, SonyPadModel model, bool bluetooth)
    {
        if (report.IsEmpty || ButtonsOffset(report[0], model, bluetooth) is not { } offset || offset >= report.Length) return null;
        return (report[offset] & HatMask) is HatLeft or HatUpLeft or HatDownLeft;
    }

    // Offset of the first button byte by report id. Over Bluetooth a pad sends a short 0x01 report until the game
    // switches it to the full one (DualSense 0x31, DualShock 4 0x11), which has extra header bytes.
    private static int? ButtonsOffset(byte reportId, SonyPadModel model, bool bluetooth) => (model, reportId) switch
    {
        (SonyPadModel.DualSense, 0x01) => bluetooth ? 5 : 8,
        (SonyPadModel.DualSense, 0x31) => 9,
        (SonyPadModel.DualShock4, 0x01) => 5,
        (SonyPadModel.DualShock4, 0x11) => 7,
        _ => null,
    };
}
