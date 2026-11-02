namespace D4Assist.Input;

/// <summary>A gamepad collection's hat switch and the parsing data its reports are read with (HidDevices.OpenHat).</summary>
public sealed class HidHat(IntPtr preparsed, int reportLength, ushort linkCollection, int logicalMin, int logicalMax) : IDisposable
{
    internal IntPtr Preparsed { get; private set; } = preparsed;
    public int ReportLength { get; } = reportLength;
    internal ushort LinkCollection { get; } = linkCollection;
    public int LogicalMin { get; } = logicalMin;
    public int LogicalMax { get; } = logicalMax;

    /// <summary>True while the report's hat points left; null for a report without the hat.</summary>
    public bool? IsLeft(byte[] report, int length) =>
        HidDevices.HatValue(this, report, length) is { } value ? HatSwitch.IsLeft(value, LogicalMin, LogicalMax) : null;

    public void Dispose()
    {
        if (Preparsed == IntPtr.Zero) return;
        HidDevices.Free(Preparsed);
        Preparsed = IntPtr.Zero;
    }
}
