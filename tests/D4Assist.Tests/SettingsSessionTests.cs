using D4Assist.Config;

namespace D4Assist.Tests;

public sealed class SettingsSessionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("d4assist-session").FullName;
    private readonly LiveSoundMix _live = new(SoundMix.Default);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string FilePath => Path.Combine(_dir, AppConfig.FileName);

    private SettingsSession NewSession(AppConfig? saved = null) => new(saved ?? new AppConfig(), FilePath, _live);

    [Fact]
    public void SoundChanged_publishes_the_working_sound_without_saving()
    {
        var session = NewSession();
        int raised = 0;
        _live.Changed += _ => raised++;
        session.Working.Sound.Cue(SoundCue.Chest).Enabled = false;

        var mix = session.SoundChanged();

        Assert.Equal(1, raised);
        Assert.Same(mix, _live.Current);
        Assert.Equal(0, _live.Current.Level(SoundCue.Chest).Gain);
        Assert.False(File.Exists(FilePath));
        Assert.True(session.Saved.Sound.Cue(SoundCue.Chest).Enabled);
    }

    [Fact]
    public void Working_is_a_copy_of_the_saved_config()
    {
        var saved = new AppConfig();
        var session = NewSession(saved);

        session.Working.Sound.MasterVolume = 1;

        Assert.Equal(100, saved.Sound.MasterVolume);
    }

    [Fact]
    public void Revert_restores_the_saved_sound_and_publishes_it()
    {
        var session = NewSession();
        session.Working.Sound.Cue(SoundCue.Chest).Enabled = false;
        session.SoundChanged();
        int raised = 0;
        _live.Changed += _ => raised++;

        session.Revert();

        Assert.Equal(1, raised);
        Assert.Equal(1, _live.Current.Level(SoundCue.Chest).Gain);
        Assert.True(session.Working.Sound.Cue(SoundCue.Chest).Enabled);
    }

    [Fact]
    public void A_sound_only_save_writes_the_file_and_needs_no_restart()
    {
        var session = NewSession();
        session.Working.Sound.MasterVolume = 30;

        var error = session.Save(out bool needsRestart);

        Assert.Null(error);
        Assert.False(needsRestart);
        Assert.Equal(30, AppConfig.Load(FilePath).Sound.MasterVolume);
        Assert.Equal(30, session.Saved.Sound.MasterVolume);
    }

    [Fact]
    public void A_general_change_needs_a_restart_once_and_the_save_becomes_the_new_baseline()
    {
        var session = NewSession();
        session.Working.SpeechEnabled = false;

        Assert.Null(session.Save(out bool first));
        Assert.Null(session.Save(out bool second));

        Assert.True(first);
        Assert.False(second);
        Assert.False(AppConfig.Load(FilePath).SpeechEnabled);
    }

    [Fact]
    public void A_failed_save_returns_the_error_and_needs_no_restart()
    {
        var session = new SettingsSession(new AppConfig(), Path.Combine(_dir, "missing", AppConfig.FileName), _live);
        session.Working.SpeechEnabled = false;

        var error = session.Save(out bool needsRestart);

        Assert.False(string.IsNullOrEmpty(error));
        Assert.False(needsRestart);
        Assert.True(session.Saved.SpeechEnabled); // not treated as saved
    }

    [Fact]
    public void ResetToDefaults_keeps_the_file_only_options_and_publishes_the_default_sound()
    {
        var saved = new AppConfig
        {
            InstallPath = @"E:\D4", DefinitionsPath = "defs.json", BeaconActorNames = ["Pad"], RadarOnlyWorlds = ["World"],
            Language = "en", SpeechEnabled = false,
        };
        saved.Sound.Cue(SoundCue.Door).Enabled = false;
        var session = NewSession(saved);
        session.SoundChanged();

        session.ResetToDefaults();

        Assert.Equal(@"E:\D4", session.Working.InstallPath);
        Assert.Equal("defs.json", session.Working.DefinitionsPath);
        Assert.Equal(["Pad"], session.Working.BeaconActorNames);
        Assert.Equal(["World"], session.Working.RadarOnlyWorlds);
        Assert.Equal(AppConfig.AutoLanguage, session.Working.Language);
        Assert.True(session.Working.SpeechEnabled);
        Assert.Equal(1, _live.Current.Level(SoundCue.Door).Gain);
    }
}
