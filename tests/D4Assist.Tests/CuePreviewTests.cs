using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Assist.Ui;

namespace D4Assist.Tests;

public class CuePreviewTests
{
    private static readonly Dictionary<string, float[]> LoopSounds = new()
    {
        [InteractableClassifier.ChestSound] = Enumerable.Repeat(0.5f, 4800).ToArray(),
        [LoopTargetSelector.QuestItemSound] = Enumerable.Repeat(0.5f, 4800).ToArray(),
    };

    private static (AudioMixer Mixer, CuePreview Preview) Create(Dictionary<string, float[]>? sounds)
    {
        var mixer = new AudioMixer();
        return (mixer, new CuePreview(mixer, sounds, RadarFrame.Cardinal));
    }

    [Fact]
    public void A_cue_with_gain_starts_one_preview_voice()
    {
        var (mixer, preview) = Create(LoopSounds);

        preview.PlayCue(SoundCue.Chest, SoundMix.Default);
        preview.PlayCue(SoundCue.Chest, SoundMix.Default);

        Assert.Equal(1, mixer.ActivePreviewVoiceCount); // the second replaces the first
    }

    [Fact]
    public void A_zero_gain_cue_stops_the_preview_instead()
    {
        var (mixer, preview) = Create(LoopSounds);
        preview.PlayCue(SoundCue.Chest, SoundMix.Default);
        var settings = new SoundSettings();
        settings.Cue(SoundCue.Chest).Enabled = false;

        preview.PlayCue(SoundCue.Chest, SoundMix.From(settings));

        Assert.Equal(0, mixer.ActivePreviewVoiceCount);
    }

    [Fact]
    public void A_group_preview_uses_the_group_level_not_the_typical_cues()
    {
        var (mixer, preview) = Create(LoopSounds);
        var settings = new SoundSettings();
        settings.Cue(SoundCue.Chest).Enabled = false; // the Objects group's typical cue
        var mix = SoundMix.From(settings);

        preview.PlayGroup(SoundGroup.Objects, mix);
        Assert.Equal(1, mixer.ActivePreviewVoiceCount);

        settings.Group(SoundGroup.Objects).Enabled = false;
        preview.PlayGroup(SoundGroup.Objects, SoundMix.From(settings));
        Assert.Equal(0, mixer.ActivePreviewVoiceCount);
    }

    [Fact]
    public void A_loop_cue_without_loop_sounds_plays_nothing_but_a_synthesized_one_still_does()
    {
        var (mixer, preview) = Create(null);

        preview.PlayCue(SoundCue.Chest, SoundMix.Default);
        Assert.Equal(0, mixer.ActivePreviewVoiceCount);

        preview.PlayCue(SoundCue.RadarExit, SoundMix.Default);
        Assert.Equal(1, mixer.ActivePreviewVoiceCount);
    }

    [Theory]
    [InlineData(SoundCue.GuidePing)]
    [InlineData(SoundCue.Npc)]
    [InlineData(SoundCue.GuideArrival)]
    [InlineData(SoundCue.RadarLeft)]
    public void Synthesized_cues_preview_without_loop_sounds(SoundCue cue)
    {
        var (mixer, preview) = Create(null);

        preview.PlayCue(cue, SoundMix.Default);

        Assert.Equal(1, mixer.ActivePreviewVoiceCount);
    }

    [Fact]
    public void Master_preview_plays_and_Stop_cuts_it()
    {
        var (mixer, preview) = Create(LoopSounds);

        preview.PlayMaster();
        Assert.Equal(1, mixer.ActivePreviewVoiceCount);

        preview.Stop();
        Assert.Equal(0, mixer.ActivePreviewVoiceCount);
    }

    [Fact]
    public void Every_group_has_a_typical_cue_in_that_group()
    {
        Assert.All(Enum.GetValues<SoundGroup>(), group => Assert.Equal(group, SoundCatalog.GroupOf(CuePreview.TypicalCue(group))));
    }
}
