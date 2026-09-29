using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D4Assist.Input;

/// <summary>
/// Read-only observer of DualShock 4 / DualSense pads over HID (cable or Bluetooth), which XInput never sees.
/// Windows gives every open handle its own copy of each report, so the game's input is untouched; pads
/// plugged in later are picked up by a periodic rescan.
/// </summary>
public sealed class SonyHidPad : IGuideTogglePad, IDisposable
{
    private const int RescanIntervalMs = 3000;

    private readonly Action<string> _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, SafeFileHandle> _reading = [];
    private readonly HashSet<string> _ignored = [];  // not a Sony gamepad collection
    private readonly HashSet<string> _failed = [];   // open failure already logged
    private Timer? _timer;
    private int _scanning;
    private bool _disposed;

    public event Action? DpadLeftPressed;

    /// <param name="log">Connect, disconnect and open-failure lines (a few per session).</param>
    public SonyHidPad(Action<string> log) => _log = log;

    public void Start() => _timer ??= new Timer(_ => Rescan(), null, 0, RescanIntervalMs);

    private void Rescan()
    {
        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
        try
        {
            foreach (var path in HidDevices.PresentPaths())
            {
                lock (_gate) { if (_disposed || _reading.ContainsKey(path) || _ignored.Contains(path)) continue; }
                TryStartReading(path);
            }
        }
        finally { Interlocked.Exchange(ref _scanning, 0); }
    }

    private void TryStartReading(string path)
    {
        if (HidDevices.Describe(path) is not { } device || SonyPadReport.ModelOf(device.VendorId, device.ProductId) is not { } model)
        {
            lock (_gate) _ignored.Add(path);
            return;
        }

        var handle = HidDevices.OpenForReading(path);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            lock (_gate) if (_failed.Add(path)) _log($"Pad: cannot read the {model} (error {error}); another program may hold it exclusively.");
            return;
        }

        int reportLength = HidDevices.GamepadReportLength(handle);
        if (reportLength == 0)
        {
            handle.Dispose();
            lock (_gate) _ignored.Add(path);
            return;
        }

        lock (_gate)
        {
            if (_disposed) { handle.Dispose(); return; }
            _reading[path] = handle;
            _failed.Remove(path);
        }
        _log($"Pad: {model} over {(device.IsBluetooth ? "Bluetooth" : "USB")} connected.");
        new Thread(() => ReadLoop(device, model, handle, reportLength)) { IsBackground = true, Name = "SonyHidPad" }.Start();
    }

    private void ReadLoop(HidDeviceInfo device, SonyPadModel model, SafeFileHandle handle, int reportLength)
    {
        var report = new byte[reportLength];
        bool held = false;
        int read;
        while ((read = HidDevices.Read(handle, report)) > 0)
        {
            if (SonyPadReport.IsDpadLeft(report.AsSpan(0, read), model, device.IsBluetooth) is not { } left) continue;
            if (left && !held) DpadLeftPressed?.Invoke();
            held = left;
        }

        lock (_gate)
        {
            _reading.Remove(device.Path);
            if (!_disposed) _log($"Pad: {model} disconnected.");
        }
        handle.Dispose();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        lock (_gate)
        {
            _disposed = true;
            foreach (var handle in _reading.Values) HidDevices.CancelRead(handle);
        }
    }
}
