using System.Drawing;
using System.Windows.Forms;
using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Navigation;
using D4Assist.Strings;

namespace D4Assist.Ui;

/// <summary>
/// The settings window: one tab per sound group (switch, volume and speed per group and per cue, applied live) and a
/// General tab (applied on restart). Built from standard Win32 controls so screen readers read every one of them.
/// </summary>
public sealed class SettingsForm : Form
{
    // Long enough to wait for the last of a quick run of key presses, short enough to feel immediate.
    private const int PreviewDelayMs = 150;
    private const int SliderWidth = 220;
    private const int VolumeTicks = 10, SpeedTicks = 25;
    private static readonly string[] LanguageCodes = [AppConfig.AutoLanguage, .. SpeechLanguages.Supported.Select(SpeechLanguages.Code)];

    private readonly SettingsSession _session;
    private readonly SettingsStrings _text;
    private readonly CuePreview _preview;
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = PreviewDelayMs };
    private readonly List<Action> _refreshers = [];
    private SoundMix _mix;
    private Action? _pendingPreview;
    private bool _refreshing, _keepChanges;

    /// <summary>Raised when the user saved General options and chose to restart now.</summary>
    public event Action? RestartRequested;

    public SettingsForm(SettingsSession session, SettingsStrings text, CuePreview preview)
    {
        (_session, _text, _preview) = (session, text, preview);
        _mix = SoundMix.From(Sound);
        Text = text.WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 600);
        _previewTimer.Tick += (_, _) => PlayPendingPreview();

        var tabs = new TabControl { Dock = DockStyle.Fill, TabIndex = 0 };
        tabs.TabPages.Add(Page(text.SoundTab, MasterBox()));
        tabs.TabPages.Add(Page(text.Groups[SoundGroup.Radar],
            SoundBox(text.Groups[SoundGroup.Radar], SoundGroup.Radar, SoundCatalog.CuesOf(SoundGroup.Radar).Where(c => !SoundCatalog.IsRadarDirection(c))),
            SoundBox(text.RadarDirections, null, SoundCatalog.CuesOf(SoundGroup.Radar).Where(SoundCatalog.IsRadarDirection))));
        tabs.TabPages.Add(Page(text.Groups[SoundGroup.Objects], GroupBox(SoundGroup.Objects)));
        tabs.TabPages.Add(Page(text.Groups[SoundGroup.Quest], GroupBox(SoundGroup.Quest)));
        tabs.TabPages.Add(Page(text.GuideAndCharactersTab, GroupBox(SoundGroup.Guide), CharactersBox()));
        tabs.TabPages.Add(Page(text.GeneralTab, GeneralBox(), PausesBox()));

        var ok = Button(text.Ok, (_, _) => { if (Save()) { _keepChanges = true; Close(); } });
        var cancel = Button(text.Cancel, (_, _) => Close());
        var apply = Button(text.Apply, (_, _) => Save());
        var defaults = Button(text.Defaults, (_, _) => { _session.ResetToDefaults(); _mix = SoundMix.From(Sound); RefreshControls(); });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, TabIndex = 1, Padding = new Padding(6) };
        buttons.Controls.AddRange([ok, cancel, apply, defaults]);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(tabs); // added before the button bar so it fills what the bar leaves
        Controls.Add(buttons);
        RefreshControls();
    }

    private SoundSettings Sound => _session.Working.Sound;
    private AppConfig Config => _session.Working;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _previewTimer.Stop();
        _preview.Stop();
        if (!_keepChanges) _session.Revert();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _previewTimer.Dispose();
        base.Dispose(disposing);
    }

    private bool Save()
    {
        if (_session.Save(out bool needsRestart) is { } error)
        {
            MessageBox.Show(this, string.Format(_text.SaveFailedFormat, error), _text.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        if (needsRestart && MessageBox.Show(this, _text.RestartQuestion, _text.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            RestartRequested?.Invoke();
        return true;
    }

    private void RefreshControls()
    {
        _refreshing = true;
        try { foreach (var refresh in _refreshers) refresh(); }
        finally { _refreshing = false; }
    }

    private void SoundChanged(Action? preview = null)
    {
        _mix = _session.SoundChanged();
        if (!Sound.Preview || preview is null) return;
        _pendingPreview = preview;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void PlayPendingPreview()
    {
        _previewTimer.Stop();
        var preview = _pendingPreview;
        _pendingPreview = null;
        preview?.Invoke();
    }

    private Control MasterBox()
    {
        var table = Table(2);
        table.Controls.Add(new Label { Text = _text.MasterVolume, AutoSize = true, Anchor = AnchorStyles.Left });
        table.Controls.Add(Slider(_text.MasterVolume, CueSetting.MinVolume, CueSetting.MaxVolume, VolumeTicks,
            () => Sound.MasterVolume, v => Sound.MasterVolume = v, () => SoundChanged(_preview.PlayMaster)));
        var previewBox = Check(_text.Preview, () => Sound.Preview, v => Sound.Preview = v, () => { if (!Sound.Preview) _preview.Stop(); });
        table.Controls.Add(previewBox);
        table.SetColumnSpan(previewBox, 2);
        return Box(_text.SoundTab, table);
    }

    private Control GroupBox(SoundGroup group, Action<TableLayoutPanel>? moreRows = null) =>
        SoundBox(_text.Groups[group], group, SoundCatalog.CuesOf(group), moreRows);

    // The characters' cues, then how many NPCs tap at once, applied live like the rest of the sound.
    private Control CharactersBox() => GroupBox(SoundGroup.Characters, table => AddChoice(table, _text.NpcVoices,
        [.. Enumerable.Range(1, NpcCuePlayer.MaxVoices).Select(n => n.ToString())],
        () => Math.Clamp(Sound.NpcVoices, 1, NpcCuePlayer.MaxVoices) - 1,
        i => { Sound.NpcVoices = i + 1; SoundChanged(); }));

    // A group's switch/volume/speed row (when given), then one row per cue; the speed column only where something repeats.
    private Control SoundBox(string title, SoundGroup? group, IEnumerable<SoundCue> cues, Action<TableLayoutPanel>? moreRows = null)
    {
        var list = cues.ToList();
        bool speeds = group is { } g && SoundCatalog.Repeats(g) || list.Any(SoundCatalog.Repeats);
        var table = Table(speeds ? 3 : 2);
        table.Controls.Add(new Label());
        table.Controls.Add(Header(_text.VolumeColumn));
        if (speeds) table.Controls.Add(Header(_text.SpeedColumn));

        if (group is { } owner)
        {
            string name = _text.Groups[owner];
            AddRow(table, speeds, () => Sound.Group(owner), string.Format(_text.GroupEnabledFormat, name),
                string.Format(_text.GroupVolumeFormat, name), SoundCatalog.Repeats(owner) ? string.Format(_text.GroupSpeedFormat, name) : null,
                () => _preview.PlayGroup(owner, _mix));
        }
        foreach (var cue in list)
        {
            string name = _text.Cues[cue];
            AddRow(table, speeds, () => Sound.Cue(cue), name, string.Format(_text.CueVolumeFormat, name),
                SoundCatalog.Repeats(cue) ? string.Format(_text.CueSpeedFormat, name) : null, () => _preview.PlayCue(cue, _mix));
        }
        moreRows?.Invoke(table);
        return Box(title, table);
    }

    private void AddRow(TableLayoutPanel table, bool speeds, Func<CueSetting> setting, string checkText, string volumeName, string? speedName, Action preview)
    {
        table.Controls.Add(Check(checkText, () => setting().Enabled, v => setting().Enabled = v, () => SoundChanged(preview)));
        table.Controls.Add(Slider(volumeName, CueSetting.MinVolume, CueSetting.MaxVolume, VolumeTicks,
            () => setting().Volume, v => setting().Volume = v, () => SoundChanged(preview)));
        if (!speeds) return;
        table.Controls.Add(speedName is null ? new Label() : Slider(speedName, CueSetting.MinSpeed, CueSetting.MaxSpeed, SpeedTicks,
            () => setting().Speed, v => setting().Speed = v, () => SoundChanged(preview), _text.SpeedDescription));
    }

    private Control GeneralBox()
    {
        var table = Table(2);
        AddInstallFolder(table);
        AddChoice(table, _text.Language, [_text.LanguageAuto, .. SpeechLanguages.Supported.Select(SpeechLanguages.NativeName)],
            () => Math.Max(0, Array.IndexOf(LanguageCodes, Config.Language.Trim().ToLowerInvariant())), i => Config.Language = LanguageCodes[i]);
        AddChoice(table, _text.RadarFrame, _text.RadarFrameChoices, () => (int)Config.RadarFrame, i => Config.RadarFrame = (RadarFrame)i);
        AddChecks(table,
            Check(_text.SpeechEnabled, () => Config.SpeechEnabled, v => Config.SpeechEnabled = v),
            Check(_text.AutoAnnouncements, () => Config.AutoAnnouncementsEnabled, v => Config.AutoAnnouncementsEnabled = v),
            Check(_text.InterruptOnContextChange, () => Config.InterruptOnContextChange, v => Config.InterruptOnContextChange = v),
            Check(_text.RadarClosedDoors, () => Config.RadarClosedDoorsEnabled, v => Config.RadarClosedDoorsEnabled = v),
            Check(_text.LiveBarriers, () => Config.LiveBarriersEnabled, v => Config.LiveBarriersEnabled = v),
            Check(_text.ExplorationGuide, () => Config.ExplorationGuideEnabled, v => Config.ExplorationGuideEnabled = v),
            Check(_text.ExplorationGuideInDungeons, () => Config.ExplorationGuideInDungeons, v => Config.ExplorationGuideInDungeons = v),
            Check(_text.QuestRoutes, () => Config.QuestRoutesEnabled, v => Config.QuestRoutesEnabled = v),
            Check(_text.OutsideDungeons, () => Config.OutsideDungeonsEnabled, v => Config.OutsideDungeonsEnabled = v),
            Check(_text.CompassDirections, () => Config.CompassDirections, v => Config.CompassDirections = v),
            Check(_text.CheckForUpdates, () => Config.CheckForUpdates, v => Config.CheckForUpdates = v));
        return Box(_text.GeneralBox, table);
    }

    private Control PausesBox()
    {
        var table = Table(2);
        AddChecks(table,
            Check(_text.PauseInCutscenes, () => Config.PauseCuesInCutscenes, v => Config.PauseCuesInCutscenes = v),
            Check(_text.PauseInBackground, () => Config.PauseCuesInBackground, v => Config.PauseCuesInBackground = v),
            Check(_text.PauseInConversations, () => Config.PauseCuesInConversations, v => Config.PauseCuesInConversations = v),
            Check(_text.PauseInMenus, () => Config.PauseCuesInMenus, v => Config.PauseCuesInMenus = v));
        return Box(_text.PausesBox, table);
    }

    // Read-only: the folder only comes from the picker, which checks it holds the game.
    private void AddInstallFolder(TableLayoutPanel table)
    {
        var folder = new TextBox { ReadOnly = true, AccessibleName = _text.InstallFolder, Width = 2 * SliderWidth };
        _refreshers.Add(() => folder.Text = string.IsNullOrWhiteSpace(Config.InstallPath) ? _text.InstallFolderAuto : Config.InstallPath);
        var browse = Button(_text.Browse, (_, _) =>
        {
            if (InstallFolderPicker.Browse(this, _text, Config.InstallPath) is not { } picked) return;
            Config.InstallPath = picked;
            RefreshControls();
        });
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange([folder, browse]);
        table.Controls.Add(new Label { Text = _text.InstallFolder, AutoSize = true, Anchor = AnchorStyles.Left });
        table.Controls.Add(row);
    }

    private void AddChoice(TableLayoutPanel table, string label, IReadOnlyList<string> choices, Func<int> get, Action<int> set)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = label, Width = 2 * SliderWidth };
        combo.Items.AddRange([.. choices]);
        _refreshers.Add(() => combo.SelectedIndex = get());
        combo.SelectedIndexChanged += (_, _) => { if (!_refreshing && combo.SelectedIndex >= 0) set(combo.SelectedIndex); };
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
        table.Controls.Add(combo);
        table.SetColumnSpan(combo, table.ColumnCount - 1);
    }

    private static void AddChecks(TableLayoutPanel table, params CheckBox[] boxes)
    {
        foreach (var box in boxes)
        {
            table.Controls.Add(box);
            table.SetColumnSpan(box, 2);
        }
    }

    private CheckBox Check(string text, Func<bool> get, Action<bool> set, Action? changed = null)
    {
        var box = new AccessibleCheckBox { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
        _refreshers.Add(() => box.Checked = get());
        box.CheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            set(box.Checked);
            changed?.Invoke();
        };
        return box;
    }

    private CueSlider Slider(string name, int minimum, int maximum, int ticks, Func<int> get, Action<int> set, Action changed, string? description = null)
    {
        var slider = new CueSlider(minimum, maximum, ticks) { AccessibleName = name, AccessibleDescription = description, Width = SliderWidth };
        _refreshers.Add(() => slider.Value = Math.Clamp(get(), minimum, maximum));
        slider.ValueChanged += (_, _) =>
        {
            if (_refreshing) return;
            set(slider.Value);
            changed();
        };
        return slider;
    }

    private static TabPage Page(string title, params Control[] boxes)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        flow.Controls.AddRange(boxes);
        var page = new TabPage(title);
        page.Controls.Add(flow);
        return page;
    }

    private static GroupBox Box(string title, Control content)
    {
        var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        box.Controls.Add(content);
        return box;
    }

    private static TableLayoutPanel Table(int columns) =>
        new() { ColumnCount = columns, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, GrowStyle = TableLayoutPanelGrowStyle.AddRows };

    private static Label Header(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }
}
