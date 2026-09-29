using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D4Assist.Input;

/// <summary>A HID device's path and vendor/product ids.</summary>
public readonly record struct HidDeviceInfo(string Path, ushort VendorId, ushort ProductId)
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
    private const ushort GenericDesktopPage = 0x01, GamepadUsage = 0x05;
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

    /// <summary>Vendor and product ids, queried without read access (so it works on devices other programs hold).</summary>
    public static HidDeviceInfo? Describe(string path)
    {
        using var handle = CreateFileW(path, NoAccess, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        return HidD_GetAttributes(handle, ref attributes) ? new HidDeviceInfo(path, attributes.VendorId, attributes.ProductId) : null;
    }

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
