using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D4Assist.Input;

/// <summary>A HID device's path, vendor/product ids, and whether its top collection is a gamepad or joystick.</summary>
public readonly record struct HidDeviceInfo(string Path, ushort VendorId, ushort ProductId, bool IsGameController = false)
{
    // Classic Bluetooth HID service UUID: it is part of the path of a Bluetooth-connected pad.
    private const string BluetoothHidService = "00001124-0000-1000-8000-00805f9b34fb";

    public bool IsBluetooth => Path.Contains(BluetoothHidService, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Enumerates HID devices and reads their input reports, always shared and read-only (never exclusive, never writes).</summary>
internal static class HidDevices
{
    private const uint DigcfPresent = 0x2, DigcfDeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000, NoAccess = 0;
    private const uint FileShareReadWrite = 0x1 | 0x2, OpenExisting = 3;
    private const ushort GenericDesktopPage = 0x01, JoystickUsage = 0x04, GamepadUsage = 0x05;
    private const int HidpInput = 0;
    private static readonly IntPtr InvalidSet = new(-1);

    /// <summary>Device paths of every present HID interface.</summary>
    public static List<string> PresentPaths()
    {
        HidD_GetHidGuid(out var hidGuid);
        var paths = new List<string>();
        var set = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (set == InvalidSet) return paths;
        try
        {
            var data = new SpDeviceInterfaceData { Size = Marshal.SizeOf<SpDeviceInterfaceData>() };
            for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data); i++)
                if (PathOf(set, ref data) is { } path) paths.Add(path);
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return paths;
    }

    /// <summary>Vendor and product ids and the collection's kind, queried without read access (so it works on devices other programs
    /// hold, and keyboards and mice are never opened to read).</summary>
    public static HidDeviceInfo? Describe(string path)
    {
        using var handle = CreateFileW(path, NoAccess, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(handle, ref attributes)) return null;
        bool controller = false;
        if (HidD_GetPreparsedData(handle, out var preparsed))
        {
            controller = HidP_GetCaps(preparsed, out var caps) == HidpStatusSuccess
                && caps.UsagePage == GenericDesktopPage && caps.Usage is GamepadUsage or JoystickUsage;
            HidD_FreePreparsedData(preparsed);
        }
        return new HidDeviceInfo(path, attributes.VendorId, attributes.ProductId, controller);
    }

    /// <summary>The hat switch of a gamepad or joystick collection opened for reading; null when it has none.</summary>
    public static HidHat? OpenHat(SafeFileHandle device)
    {
        if (!HidD_GetPreparsedData(device, out var preparsed)) return null;
        var hats = new HidpValueCaps[4];
        ushort count = (ushort)hats.Length;
        if (HidP_GetCaps(preparsed, out var caps) == HidpStatusSuccess
            && HidP_GetSpecificValueCaps(HidpInput, GenericDesktopPage, 0, HatSwitch.Usage, hats, ref count, preparsed) == HidpStatusSuccess && count > 0)
            return new HidHat(preparsed, caps.InputReportByteLength, hats[0].LinkCollection, hats[0].LogicalMin, hats[0].LogicalMax);
        HidD_FreePreparsedData(preparsed);
        return null;
    }

    /// <summary>The hat's value in one input report; null for a report that doesn't carry it (another report id).</summary>
    internal static long? HatValue(HidHat hat, byte[] report, int length)
    {
        if (length != hat.ReportLength) return null;
        return HidP_GetUsageValue(HidpInput, GenericDesktopPage, hat.LinkCollection, HatSwitch.Usage, out uint value, hat.Preparsed, report, (uint)length)
            == HidpStatusSuccess ? value : null;
    }

    internal static void Free(IntPtr preparsed) => HidD_FreePreparsedData(preparsed);

    /// <summary>Opens a device to read its reports alongside the game; invalid on failure (see Marshal.GetLastPInvokeError).</summary>
    public static SafeFileHandle OpenForReading(string path) =>
        CreateFileW(path, GenericRead, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

    /// <summary>Input report size of a gamepad collection; 0 for any other kind of collection.</summary>
    public static int GamepadReportLength(SafeFileHandle device)
    {
        if (!HidD_GetPreparsedData(device, out var preparsed)) return 0;
        try
        {
            if (HidP_GetCaps(preparsed, out var caps) != HidpStatusSuccess) return 0;
            return caps.UsagePage == GenericDesktopPage && caps.Usage == GamepadUsage ? caps.InputReportByteLength : 0;
        }
        finally { HidD_FreePreparsedData(preparsed); }
    }

    /// <summary>Blocks until the next report; its byte count, or 0 once the device is gone or the read was cancelled.</summary>
    public static int Read(SafeFileHandle device, byte[] buffer) =>
        ReadFile(device, buffer, buffer.Length, out int read, IntPtr.Zero) ? read : 0;

    public static void CancelRead(SafeFileHandle device) => CancelIoEx(device, IntPtr.Zero);

    private static string? PathOf(IntPtr set, ref SpDeviceInterfaceData data)
    {
        SetupDiGetDeviceInterfaceDetailW(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
        if (size <= 0) return null;
        var detail = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6); // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize
            return SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, size, out _, IntPtr.Zero)
                ? Marshal.PtrToStringUni(detail + sizeof(int))
                : null;
        }
        finally { Marshal.FreeHGlobal(detail); }
    }

    private const int HidpStatusSuccess = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId, ProductId, VersionNumber;
    }

    // HIDP_CAPS: five USHORTs, USHORT Reserved[17], then ten USHORT counts.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    // HidD_* return a one-byte BOOLEAN, not a four-byte BOOL.
    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    // HIDP_VALUE_CAPS (hidpi.h), 72 bytes: only the fields read here.
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    private struct HidpValueCaps
    {
        [FieldOffset(6)] public ushort LinkCollection;
        [FieldOffset(40)] public int LogicalMin;
        [FieldOffset(44)] public int LogicalMax;
    }

    [DllImport("hid.dll")]
    private static extern int HidP_GetSpecificValueCaps(
        int reportType, ushort usagePage, ushort linkCollection, ushort usage, [Out] HidpValueCaps[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsageValue(
        int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData data);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        IntPtr deviceInfoSet, ref SpDeviceInterfaceData data, IntPtr detail, int detailSize, out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle file, byte[] buffer, int bytesToRead, out int bytesRead, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    private static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
}
