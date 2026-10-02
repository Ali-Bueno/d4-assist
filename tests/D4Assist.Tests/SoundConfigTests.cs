using D4Assist.Audio;
using D4Assist.Config;

namespace D4Assist.Tests;

public sealed class SoundConfigTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("d4assist-config").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name = AppConfig.FileName) => Path.Combine(_dir, name);

    [Fact]
    public void Save_then_Load_round_trips_the_sound_section_and_general_options()
    {
        var config = new AppConfig { Language = "en", SpeechEnabled = false, RadarFrame = RadarFrame.Diagonal, BeaconActorNames = ["A", "B"] };
        config.Sound.MasterVolume = 40;
        config.Sound.Preview = false;
        config.Sound.NpcVoices = 3;
        config.Sound.Group(SoundGroup.Radar).Volume = 30;
        config.Sound.Cue(SoundCue.Chest).Enabled = false;
        config.Sound.Cue(SoundCue.GuidePing).Speed = 250;

        config.Save(PathOf());
        var loaded = AppConfig.Load(PathOf());

        Assert.Equal("en", loaded.Language);
        Assert.False(loaded.SpeechEnabled);
        Assert.Equal(RadarFrame.Diagonal, loaded.RadarFrame);
        Assert.Equal(["A", "B"], loaded.BeaconActorNames);
        Assert.Equal((40, false, 3), (loaded.Sound.MasterVolume, loaded.Sound.Preview, loaded.Sound.NpcVoices));
        Assert.Equal(3, loaded.Sound.Clone().NpcVoices);
        Assert.Equal(30, loaded.Sound.Group(SoundGroup.Radar).Volume);
        Assert.False(loaded.Sound.Cue(SoundCue.Chest).Enabled);
        Assert.Equal(250, loaded.Sound.Cue(SoundCue.GuidePing).Speed);
        Assert.False(File.Exists(PathOf() + ".tmp"));
    }

    [Fact]
    public void Save_lists_every_group_and_cue()
    {
        new AppConfig().Save(PathOf());

        var loaded = AppConfig.Load(PathOf());

        Assert.Equal(Enum.GetValues<SoundGroup>().Length, loaded.Sound.Groups.Count);
        Assert.Equal(Enum.GetValues<SoundCue>().Length, loaded.Sound.Cues.Count);
    }

    [Fact]
    public void A_file_with_only_some_sound_entries_keeps_the_defaults_for_the_rest()
    {
        File.WriteAllText(PathOf(), """{ "Sound": { "MasterVolume": 60, "Cues": { "Door": { "Volume": 20 } } } }""");

        var sound = AppConfig.Load(PathOf()).Sound;

        Assert.Equal(60, sound.MasterVolume);
        Assert.Equal(20, sound.Cue(SoundCue.Door).Volume);
        Assert.True(sound.Cue(SoundCue.Door).Enabled); // a missing field inside an entry
        Assert.Equal(new CueSetting().Volume, sound.Cue(SoundCue.Chest).Volume);
        Assert.Equal(CueLevel.Full, SoundMix.From(sound).Level(SoundCue.Npc));
    }

    [Fact]
    public void A_file_without_a_sound_section_loads_the_defaults()
    {
        File.WriteAllText(PathOf(), """{ "Language": "es" }""");

        Assert.Equal(CueLevel.Full, SoundMix.From(AppConfig.Load(PathOf()).Sound).Level(SoundCue.Chest));
    }

    [Fact]
    public void Unknown_keys_and_null_entries_do_not_break_loading()
    {
        File.WriteAllText(PathOf(), """{ "Sound": { "Cues": { "NoSuchCue": { "Volume": 5 }, "Chest": null }, "Groups": { "Nope": { "Enabled": false } } } }""");

        var sound = AppConfig.Load(PathOf()).Sound;

        Assert.Equal(1, SoundMix.From(sound).Level(SoundCue.Chest).Gain);
        Assert.Equal(1, SoundMix.From(sound).GroupLevel(SoundGroup.Radar).Gain);
    }

    [Fact]
    public void Entry_keys_are_case_insensitive()
    {
        File.WriteAllText(PathOf(), """{ "Sound": { "Cues": { "chest": { "Enabled": false } }, "Groups": { "RADAR": { "Volume": 0 } } } }""");

        var mix = SoundMix.From(AppConfig.Load(PathOf()).Sound);

        Assert.Equal(0, mix.Level(SoundCue.Chest).Gain);
        Assert.Equal(0, mix.GroupLevel(SoundGroup.Radar).Gain);
    }

    [Fact]
    public void SameStartupOptions_ignores_sound_changes_but_catches_a_general_one()
    {
        var saved = new AppConfig();
        var soundOnly = saved.Clone();
        soundOnly.Sound.MasterVolume = 10;
        soundOnly.Sound.Cue(SoundCue.Door).Enabled = false;
        var general = saved.Clone();
        general.SpeechEnabled = !saved.SpeechEnabled;

        Assert.True(saved.SameStartupOptions(soundOnly));
        Assert.False(saved.SameStartupOptions(general));
    }

    [Fact]
    public void Clone_is_independent()
    {
        var original = new AppConfig();
        original.Sound.Cue(SoundCue.Chest).Volume = 70;
        original.BeaconActorNames = ["X"];

        var copy = original.Clone();
        copy.Sound.Cue(SoundCue.Chest).Volume = 10;
        copy.Sound.MasterVolume = 5;
        copy.BeaconActorNames[0] = "Y";

        Assert.Equal(70, original.Sound.Cue(SoundCue.Chest).Volume);
        Assert.Equal(SoundSettings.DefaultMasterVolume, original.Sound.MasterVolume);
        Assert.Equal(["X"], original.BeaconActorNames);
    }

    [Fact]
    public void SoundSettings_Clone_copies_entries_and_keeps_case_insensitive_keys()
    {
        var sound = new SoundSettings();
        sound.Cue(SoundCue.Door).Speed = 200;

        var copy = sound.Clone();
        copy.Cue(SoundCue.Door).Speed = 50;

        Assert.Equal(200, sound.Cue(SoundCue.Door).Speed);
        Assert.True(copy.Cues.ContainsKey("door"));
    }
}
