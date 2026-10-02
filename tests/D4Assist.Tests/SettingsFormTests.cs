using System.Runtime.InteropServices;
using System.Windows.Forms;
using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Strings;
using D4Assist.Ui;

namespace D4Assist.Tests;

public class SettingsFormTests
{
    [Fact]
    public void EverySliderAndComboHasAScreenReaderName_EveryTabIsThere()
    {
        var (tabs, sliders, combos, checks) = OnUiThread(form =>
        {
            var all = Descendants(form).ToList();
            return (
                all.OfType<TabControl>().Single().TabPages.Cast<TabPage>().Select(p => p.Text).ToList(),
                all.OfType<CueSlider>().ToList(),
                all.OfType<ComboBox>().ToList(),
                all.OfType<CheckBox>().ToList());
        });

        Assert.Equal(6, tabs.Count);
        Assert.All(sliders, s => Assert.False(string.IsNullOrWhiteSpace(s.AccessibleName)));
        Assert.All(combos, c => Assert.False(string.IsNullOrWhiteSpace(c.AccessibleName)));
        Assert.All(checks, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
        Assert.Equal(sliders.Count, sliders.Select(s => s.AccessibleName).Distinct().Count());
    }

    [Fact]
    public void OneVolumePerGroupAndCue_OneSpeedPerRepeatingGroupAndCue_PlusTheMaster()
    {
        var sliders = OnUiThread(form => Descendants(form).OfType<CueSlider>().ToList());

        int volumes = Enum.GetValues<SoundGroup>().Length + Enum.GetValues<SoundCue>().Length + 1;
        int speeds = Enum.GetValues<SoundGroup>().Count(SoundCatalog.Repeats) + Enum.GetValues<SoundCue>().Count(SoundCatalog.Repeats);
        Assert.Equal(volumes, sliders.Count(s => s.Maximum == CueSetting.MaxVolume));
        Assert.Equal(speeds, sliders.Count(s => s.Maximum == CueSetting.MaxSpeed));
    }

    [Fact]
    public void MovingASliderPlaysTheSoundLive_ClosingWithoutSavingRestoresIt()
    {
        var live = new LiveSoundMix(SoundMix.Default);
        var gains = new List<double>();
        live.Changed += mix => gains.Add(mix.Level(SoundCue.Chest).Gain);

        OnUiThread(form =>
        {
            var chest = Descendants(form).OfType<CueSlider>()
                .Single(s => s.AccessibleName == string.Format(SettingsStrings.Spanish.CueVolumeFormat, SettingsStrings.Spanish.Cues[SoundCue.Chest]));
            _ = form.Handle; // a form without a window closes without its closing events
            chest.Value = 50;
            form.Close();
            return 0;
        }, live);

        Assert.Equal([0.25, 1.0], gains);
    }

    [Fact]
    public void AScreenReaderReadsTheSpeedItself_NotItsPlaceInTheRange()
    {
        var read = OnUiThread(_ =>
        {
            using var slider = new CueSlider(CueSetting.MinSpeed, CueSetting.MaxSpeed, 25) { Value = CueSetting.NormalSpeed };
            slider.CreateControl();
            var values = new List<string?> { slider.AccessibilityObject.Value };
            foreach (int value in new[] { CueSetting.MaxSpeed, CueSetting.MinSpeed })
            {
                slider.Value = value;
                values.Add(slider.AccessibilityObject.Value);
            }
            return (values, slider.AccessibilityObject.Role, slider.Minimum, slider.Maximum);
        });

        Assert.Equal(["100", "300", "25"], read.values);
        Assert.Equal((AccessibleRole.Slider, CueSetting.MinSpeed, CueSetting.MaxSpeed), (read.Role, read.Minimum, read.Maximum));
    }

    [Fact]
    public void TogglingACheckBoxTellsTheScreenReaderItsNewState()
    {
        var heard = OnUiThread(form =>
        {
            bool allAccessible = Descendants(form).OfType<CheckBox>().All(c => c is AccessibleCheckBox);
            using var box = new AccessibleCheckBox { Text = "x" };
            box.CreateControl();
            var events = new List<(IntPtr Window, int ObjectId, int ChildId)>();
            WinEventProc proc = (_, _, window, objectId, childId, _, _) => events.Add((window, objectId, childId));
            IntPtr hook = SetWinEventHook(StateChangeEvent, StateChangeEvent, IntPtr.Zero, proc, (uint)Environment.ProcessId, GetCurrentThreadId(), 0);
            try
            {
                box.Checked = true;
                for (int i = 0; i < 50 && events.Count == 0; i++) { Application.DoEvents(); Thread.Sleep(10); }
            }
            finally { UnhookWinEvent(hook); GC.KeepAlive(proc); }
            return (allAccessible, uiaProvider: UiaHasServerSideProvider(box.Handle), events, box.Handle, box.AccessibilityObject.State);
        });

        Assert.True(heard.allAccessible);
        Assert.False(heard.uiaProvider); // no UIA provider: NVDA reads the MSAA object, which gets the event
        Assert.Contains((heard.Handle, -4, 0), heard.events); // OBJID_CLIENT, CHILDID_SELF
        Assert.True(heard.State.HasFlag(AccessibleStates.Checked));
    }

    private const uint StateChangeEvent = 0x800A; // winuser.h EVENT_OBJECT_STATECHANGE
    private delegate void WinEventProc(IntPtr hook, uint winEvent, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("UIAutomationCore.dll")] private static extern bool UiaHasServerSideProvider(IntPtr window);

    private static T OnUiThread<T>(Func<SettingsForm, T> use, LiveSoundMix? live = null)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"d4assist-form-{Guid.NewGuid():N}.json");
                var session = new SettingsSession(new AppConfig(), path, live ?? new LiveSoundMix(SoundMix.Default));
                using var form = new SettingsForm(session, SettingsStrings.Spanish, new CuePreview(new AudioMixer(), null, RadarFrame.Cardinal));
                result = use(form);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        return result;
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var grandchild in Descendants(child)) yield return grandchild;
        }
    }
}
