using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D4Assist.Input;

/// <summary>
/// Read-only observer of HID gamepads' D-pads: DualShock 4 / DualSense pads (which XInput never sees) by their report layout, and
/// any other pad by its hat switch, which also reaches an Xbox pad's HID side when XInput gives a background app no buttons (the
/// Xbox full screen experience on a ROG Xbox Ally, tester 2026-11-01). Windows gives every open handle its own copy of each report,
/// so the game's input is untouched; pads plugged in later are picked up by a periodic rescan.
/// </summary>
public sealed class HidPad : IGuideTogglePad, IDisposable
{
    private const int RescanIntervalMs = 3000;

    // One pad's last D-pad state, written by its read thread and sampled on the poll clock: a pad that reports only on change
    // still reads a hold on time.
    private sealed class Pad(string name)
    {
        public string Name { get; } = name;
        public volatile bool Left;
        public bool LeftSeen, ReportSeen, HatMoved;
        public long? FirstHat;
        public PressHold Button { get; } = new();
    }

    private readonly Action<string> _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, (SafeFileHandle Handle, Pad Pad)> _reading = [];
    private readonly HashSet<string> _ignored = [];  // not a gamepad, or no D-pad we can read
    private readonly HashSet<string> _failed = [];   // open failure already logged
    private Timer? _rescanTimer, _pollTimer;
    private int _scanning, _polling;
    private bool _disposed;

    public event Action? DpadLeftPressed;
    public event Action? DpadLeftHeld;

    /// <param name="log">Connect, disconnect, open-failure and first-press lines (a few per session).</param>
    public HidPad(Action<string> log) => _log = log;

    public void Start()
    {
        _rescanTimer ??= new Timer(_ => Rescan(), null, 0, RescanIntervalMs);
        _pollTimer ??= new Timer(_ => Poll(), null, 0, XInputPad.PollIntervalMs);
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try
        {
            Pad[] pads;
            lock (_gate) pads = [.. _reading.Values.Select(r => r.Pad)];
            long now = Environment.TickCount64;
            foreach (var pad in pads)
            {
                var edge = pad.Button.Sample(pad.Left, now);
                if (edge == PressHold.Edge.Pressed) DpadLeftPressed?.Invoke();
                else if (edge == PressHold.Edge.Held) DpadLeftHeld?.Invoke();
            }
        }
        finally { Interlocked.Exchange(ref _polling, 0); }
    }

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
        var device = HidDevices.Describe(path);
        var sony = device is { } d ? SonyPadReport.ModelOf(d.VendorId, d.ProductId) : null;
        if (device is not { } info || (sony is null && !info.IsGameController))
        {
            lock (_gate) _ignored.Add(path);
            return;
        }
        string name = sony?.ToString() ?? $"HID gamepad 0x{info.VendorId:X4}:0x{info.ProductId:X4}";

        var handle = HidDevices.OpenForReading(path);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            lock (_gate) if (_failed.Add(path)) _log($"Pad: cannot read the {name} (error {error}); another program may hold it exclusively.");
            return;
        }

        int reportLength = sony is not null ? HidDevices.GamepadReportLength(handle) : 0;
        var hat = sony is null ? HidDevices.OpenHat(handle) : null;
        if (sony is not null ? reportLength == 0 : hat is null)
        {
            handle.Dispose();
            lock (_gate) _ignored.Add(path);
            if (sony is null) _log($"Pad: {name} has no hat switch; its D-pad can't be read.");
            return;
        }

        var pad = new Pad(name);
        lock (_gate)
        {
            if (_disposed) { handle.Dispose(); hat?.Dispose(); return; }
            _reading[path] = (handle, pad);
            _failed.Remove(path);
        }
        _log(sony is not null
            ? $"Pad: {name} over {(info.IsBluetooth ? "Bluetooth" : "USB")} connected."
            : $"Pad: {name} connected (hat {hat!.LogicalMin}-{hat.LogicalMax}).");
        new Thread(() => ReadLoop(info, sony, hat, handle, pad, sony is not null ? reportLength : hat!.ReportLength))
            { IsBackground = true, Name = "HidPad" }.Start();
    }

    private void ReadLoop(HidDeviceInfo device, SonyPadModel? sony, HidHat? hat, SafeFileHandle handle, Pad pad, int reportLength)
    {
        var report = new byte[reportLength];
        int read;
        while ((read = HidDevices.Read(handle, report)) > 0)
        {
            long? hatValue = hat?.Value(report, read);
            if (hat is not null) NoteReport(pad, report, read, hatValue);
            bool? left = sony is { } model ? SonyPadReport.IsDpadLeft(report.AsSpan(0, read), model, device.IsBluetooth)
                : hatValue is { } value ? hat!.IsLeft(value) : null;
            if (left is not { } now) continue;
            pad.Left = now;
            if (!now || pad.LeftSeen) continue;
            pad.LeftSeen = true;
            _log($"Pad: {pad.Name} first D-pad left.");
        }

        lock (_gate)
        {
            _reading.Remove(device.Path);
            if (!_disposed) _log($"Pad: {pad.Name} disconnected.");
        }
        handle.Dispose();
        hat?.Dispose();
    }

    // Once per connection: a pad whose hat never moves is told from one whose reports lack it and one that sends nothing (the
    // ROG Xbox Ally's in the Xbox full screen experience, tester 2026-11-01).
    private void NoteReport(Pad pad, byte[] report, int length, long? hat)
    {
        if (!pad.ReportSeen)
        {
            pad.ReportSeen = true;
            _log($"Pad: {pad.Name} first report: {length} bytes, id {report[0]}, hat {(hat is { } h ? h.ToString() : "none")}.");
        }
        if (hat is not { } value) return;
        if (pad.FirstHat is not { } first) pad.FirstHat = value;
        else if (!pad.HatMoved && value != first)
        {
            pad.HatMoved = true;
            _log($"Pad: {pad.Name} hat moved to {value}.");
        }
    }

    public void Dispose()
    {
        _rescanTimer?.Dispose();
        _pollTimer?.Dispose();
        lock (_gate)
        {
            _disposed = true;
            foreach (var (handle, _) in _reading.Values) HidDevices.CancelRead(handle);
        }
    }
}
