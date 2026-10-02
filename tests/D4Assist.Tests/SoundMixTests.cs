using D4Assist.Audio;
using D4Assist.Config;

namespace D4Assist.Tests;

public class SoundMixTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Default_is_half_master_volume_full_cues_but_the_scroll_and_normal_rate()
    {
        var mix = SoundMix.From(new SoundSettings());

        Assert.Equal(0.25, mix.Master, Tolerance); // 50 on the squared curve
        Assert.All(Enum.GetValues<SoundCue>().Where(c => c != SoundCue.Readable), cue => Assert.Equal(CueLevel.Full, mix.Level(cue)));
        Assert.Equal(SoundMix.From(new SoundSettings { Cues = new() { ["Readable"] = new CueSetting { Volume = 50 } } }).Level(SoundCue.Readable),
            mix.Level(SoundCue.Readable));
        Assert.All(Enum.GetValues<SoundGroup>(), group => Assert.Equal(CueLevel.Full, mix.GroupLevel(group)));
        Assert.Equal(1, mix.RadarGain(RadarCueKind.Exit, RadarRay.Up));
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(50, 0.25)]
    [InlineData(0, 0.0)]
    [InlineData(-20, 0.0)]
    [InlineData(250, 1.0)]
    public void Volume_is_squared_and_clamped(int volume, double amplitude) =>
        Assert.Equal(amplitude, SoundMix.Amplitude(volume), Tolerance);

    [Theory]
    [InlineData(null, NpcCuePlayer.DefaultVoices)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(9, NpcCuePlayer.MaxVoices)]
    public void Npc_voices_default_to_two_and_clamp(int? voices, int expected)
    {
        var settings = new SoundSettings();
        if (voices is { } v) settings.NpcVoices = v;

        Assert.Equal(expected, SoundMix.From(settings).NpcVoices);
    }

    [Fact]
    public void Master_follows_the_master_volume() =>
        Assert.Equal(0.25, SoundMix.From(new SoundSettings { MasterVolume = 50 }).Master, Tolerance);

    [Fact]
    public void A_cue_and_its_group_multiply()
    {
        var settings = new SoundSettings();
        settings.Group(SoundGroup.Objects).Volume = 50;
        settings.Cue(SoundCue.Chest).Volume = 50;

        var mix = SoundMix.From(settings);

        Assert.Equal(0.0625, mix.Level(SoundCue.Chest).Gain, Tolerance);
        Assert.Equal(0.25, mix.Level(SoundCue.Door).Gain, Tolerance); // the group alone
        Assert.Equal(1, mix.Level(SoundCue.Portal).Gain);             // another group
    }

    [Fact]
    public void A_disabled_group_or_cue_is_silent()
    {
        var settings = new SoundSettings();
        settings.Group(SoundGroup.Quest).Enabled = false;
        settings.Cue(SoundCue.Door).Enabled = false;

        var mix = SoundMix.From(settings);

        Assert.All(SoundCatalog.CuesOf(SoundGroup.Quest), cue => Assert.Equal(0, mix.Level(cue).Gain));
        Assert.Equal(0, mix.GroupLevel(SoundGroup.Quest).Gain);
        Assert.Equal(0, mix.Level(SoundCue.Door).Gain);
        Assert.Equal(1, mix.Level(SoundCue.Chest).Gain);
    }

    [Theory]
    [InlineData(100, 100, 1.0)]
    [InlineData(200, 50, 1.0)]
    [InlineData(300, 300, 9.0)]
    [InlineData(1000, 100, 3.0)] // clamped to the top
    [InlineData(1, 100, 0.25)]   // clamped to the bottom
    public void Speeds_clamp_then_multiply(int groupSpeed, int cueSpeed, double rate)
    {
        var settings = new SoundSettings();
        settings.Group(SoundGroup.Guide).Speed = groupSpeed;
        settings.Cue(SoundCue.GuidePing).Speed = cueSpeed;

        Assert.Equal(rate, SoundMix.From(settings).Level(SoundCue.GuidePing).Rate, Tolerance);
    }

    [Fact]
    public void Radar_gain_combines_the_kind_and_the_direction_without_counting_the_group_twice()
    {
        var settings = new SoundSettings();
        settings.Group(SoundGroup.Radar).Volume = 50;   // 0.25
        settings.Cue(SoundCue.RadarWall).Volume = 50;   // 0.25
        settings.Cue(SoundCue.RadarLeft).Volume = 50;   // 0.25 (own amplitude only)

        var mix = SoundMix.From(settings);

        Assert.Equal(0.25 * 0.25 * 0.25, mix.RadarGain(RadarCueKind.Blocked, RadarRay.Left), Tolerance);
        Assert.Equal(0.25 * 0.25, mix.RadarGain(RadarCueKind.Blocked, RadarRay.Up), Tolerance);
    }

    [Fact]
    public void A_disabled_direction_silences_that_side_only()
    {
        var settings = new SoundSettings();
        settings.Cue(SoundCue.RadarBack).Enabled = false;

        var mix = SoundMix.From(settings);

        Assert.Equal(0, mix.RadarGain(RadarCueKind.Exit, RadarRay.Down));
        Assert.Equal(1, mix.RadarGain(RadarCueKind.Exit, RadarRay.Up));
    }

    [Fact]
    public void Group_level_is_the_group_entry_alone()
    {
        var settings = new SoundSettings();
        settings.Group(SoundGroup.Guide).Volume = 50;
        settings.Group(SoundGroup.Guide).Speed = 200;
        settings.Cue(SoundCue.GuidePing).Volume = 0;

        var level = SoundMix.From(settings).GroupLevel(SoundGroup.Guide);

        Assert.Equal(new CueLevel(0.25, 2.0), level);
    }

    [Fact]
    public void Live_mix_publishes_to_subscribers_and_updates_current()
    {
        var live = new LiveSoundMix(SoundMix.Default);
        var seen = new List<SoundMix>();
        live.Changed += seen.Add;
        var next = SoundMix.From(new SoundSettings { MasterVolume = 10 });

        live.Publish(next);

        Assert.Same(next, live.Current);
        Assert.Equal([next], seen);
    }
}
