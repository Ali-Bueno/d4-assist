using D4Assist.Input;

namespace D4Assist.Tests;

public class SonyPadTests
{
    private const byte Released = 0x08, Up = 0x00, Right = 0x02, DownLeft = 0x05, Left = 0x06, UpLeft = 0x07;

    [Theory]
    [InlineData(SonyPadModel.DualSense, false, 0x01, 8)]  // USB
    [InlineData(SonyPadModel.DualSense, true, 0x31, 9)]   // Bluetooth, full report
    [InlineData(SonyPadModel.DualSense, true, 0x01, 5)]   // Bluetooth, short report
    [InlineData(SonyPadModel.DualShock4, false, 0x01, 5)] // USB
    [InlineData(SonyPadModel.DualShock4, true, 0x11, 7)]  // Bluetooth, full report
    [InlineData(SonyPadModel.DualShock4, true, 0x01, 5)]  // Bluetooth, short report
    public void ReadsTheHatFromEachReportLayout(SonyPadModel model, bool bluetooth, byte reportId, int buttonsOffset)
    {
        var report = new byte[78];
        report[0] = reportId;

        report[buttonsOffset] = 0x80 | Left; // a face button held too (high nibble) must not matter
        Assert.True(SonyPadReport.IsDpadLeft(report, model, bluetooth));
        report[buttonsOffset] = Released;
        Assert.False(SonyPadReport.IsDpadLeft(report, model, bluetooth));
    }

    [Theory]
    [InlineData(Left, true)]
    [InlineData(UpLeft, true)]
    [InlineData(DownLeft, true)]
    [InlineData(Up, false)]
    [InlineData(Right, false)]
    [InlineData(Released, false)]
    public void CountsLeftAndItsDiagonalsLikeXInput(byte hat, bool left)
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[8] = hat;

        Assert.Equal(left, SonyPadReport.IsDpadLeft(report, SonyPadModel.DualSense, bluetooth: false));
    }

    [Fact]
    public void IgnoresReportsWithoutButtons()
    {
        var report = new byte[64];
        report[0] = 0x05; // e.g. a feature/status report id

        Assert.Null(SonyPadReport.IsDpadLeft(report, SonyPadModel.DualSense, bluetooth: false));
        Assert.Null(SonyPadReport.IsDpadLeft([], SonyPadModel.DualSense, bluetooth: false));
    }

    [Theory]
    [InlineData(0x054C, 0x0CE6, SonyPadModel.DualSense)]
    [InlineData(0x054C, 0x0DF2, SonyPadModel.DualSense)]
    [InlineData(0x054C, 0x09CC, SonyPadModel.DualShock4)]
    [InlineData(0x054C, 0x05C4, SonyPadModel.DualShock4)]
    public void KnowsSonyPads(int vendor, int product, SonyPadModel model) =>
        Assert.Equal(model, SonyPadReport.ModelOf((ushort)vendor, (ushort)product));

    [Fact]
    public void IgnoresOtherDevices()
    {
        Assert.Null(SonyPadReport.ModelOf(0x045E, 0x0B13)); // Xbox controller
        Assert.Null(SonyPadReport.ModelOf(0x054C, 0x0001));
    }

    [Fact]
    public void CombinedPad_DropsTheSamePressFromASecondSource()
    {
        var (xbox, sony) = (new FakePad(), new FakePad());
        long now = 1000;
        var pad = new CombinedPad([xbox, sony], () => now);
        int presses = 0;
        pad.DpadLeftPressed += () => presses++;

        sony.Press();
        now += 30;
        xbox.Press(); // the same press through a virtual Xbox pad
        Assert.Equal(1, presses);

        now += CombinedPad.SamePressWindowMs;
        sony.Press();
        Assert.Equal(2, presses);
    }

    private sealed class FakePad : IGuideTogglePad
    {
        public event Action? DpadLeftPressed;
        public void Press() => DpadLeftPressed?.Invoke();
    }
}
