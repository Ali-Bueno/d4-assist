namespace D4Assist.Config;

/// <summary>One visit to the settings window: edits a working copy, plays sound changes live, and saves or reverts.</summary>
/// <param name="saved">The config as last saved; never modified in place.</param>
public sealed class SettingsSession(AppConfig saved, string path, LiveSoundMix live)
{
    private AppConfig _saved = saved;

    /// <summary>The copy the window edits; replaced by <see cref="Revert"/> and <see cref="ResetToDefaults"/>.</summary>
    public AppConfig Working { get; private set; } = saved.Clone();

    /// <summary>The config as last saved, for the next visit.</summary>
    public AppConfig Saved => _saved;

    /// <summary>Makes the working sound settings audible right away (not saved yet) and returns the mix now playing.</summary>
    public SoundMix SoundChanged()
    {
        var mix = SoundMix.From(Working.Sound);
        live.Publish(mix);
        return mix;
    }

    /// <summary>Saves the working copy. Returns null on success, else the error; <paramref name="needsRestart"/> tells whether an
    /// option outside the sound section changed.</summary>
    public string? Save(out bool needsRestart)
    {
        needsRestart = false;
        try { Working.Save(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return e.Message; }
        needsRestart = !Working.SameStartupOptions(_saved);
        _saved = Working.Clone();
        return null;
    }

    /// <summary>Drops the unsaved changes and plays the saved sound again.</summary>
    public void Revert()
    {
        Working = _saved.Clone();
        SoundChanged();
    }

    /// <summary>Every option back to its default, except the file-only ones (install and data paths, beacon and world lists).</summary>
    public void ResetToDefaults()
    {
        Working = new AppConfig
        {
            InstallPath = Working.InstallPath,
            DefinitionsPath = Working.DefinitionsPath,
            BeaconActorNames = Working.BeaconActorNames,
            RadarOnlyWorlds = Working.RadarOnlyWorlds,
        };
        SoundChanged();
    }
}
