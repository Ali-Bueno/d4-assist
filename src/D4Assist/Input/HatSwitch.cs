namespace D4Assist.Input;

/// <summary>Decodes a HID hat switch (usage page Generic Desktop, usage 0x39): its first position points up, the rest go clockwise.</summary>
public static class HatSwitch
{
    // HID Usage Tables, Generic Desktop page.
    public const ushort Usage = 0x39;

    // Left, up-left and down-left (like XInput's DPAD_LEFT), in degrees clockwise from up.
    private const float LeftFromDegrees = 225f, LeftToDegrees = 315f;

    /// <summary>True while the hat points left, up-left or down-left; false when centred (a value outside its logical range).</summary>
    public static bool IsLeft(long value, int logicalMin, int logicalMax)
    {
        int positions = logicalMax - logicalMin + 1;
        if (positions < 4 || value < logicalMin || value > logicalMax) return false;
        float degrees = (value - logicalMin) * 360f / positions;
        return degrees is >= LeftFromDegrees and <= LeftToDegrees;
    }
}
