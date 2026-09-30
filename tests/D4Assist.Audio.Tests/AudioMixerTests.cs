namespace D4Assist.Audio.Tests;

/// <summary>AudioMixer's own contract: pan balance, clipping and voice stealing. Never opens a device —
/// AudioMixer only produces PCM buffers, IAudioOut is a separate concern.</summary>
public class AudioMixerTests
{
    private static float[] Tone(int frames, float amplitude = 1f) => Enumerable.Repeat(amplitude, frames).ToArray();

    [Fact]
    public void HardLeftPan_SilencesTheRightChannel()
    {
        var mixer = new AudioMixer();
        mixer.Play(Tone(100), gain: 1.0, pan: -1.0);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.True(buffer[0] > 0, "Left channel should carry the tone.");
        Assert.Equal(0, buffer[1]);
    }

    [Fact]
    public void HardRightPan_SilencesTheLeftChannel()
    {
        var mixer = new AudioMixer();
        mixer.Play(Tone(100), gain: 1.0, pan: 1.0);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.Equal(0, buffer[0]);
        Assert.True(buffer[1] > 0, "Right channel should carry the tone.");
    }

    [Fact]
    public void CenterPan_BalancesBothChannels()
    {
        var mixer = new AudioMixer();
        mixer.Play(Tone(100), gain: 1.0, pan: 0.0);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.Equal(buffer[0], buffer[1]);
        Assert.True(buffer[0] > 0);
    }

    [Fact]
    public void OverlappingVoices_ClipInsteadOfWrapping()
    {
        var mixer = new AudioMixer();
        for (int i = 0; i < 5; i++) mixer.Play(Tone(100, amplitude: 1f), gain: 1.0, pan: 0.0);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        // Five full-scale centered voices would overflow int16 if not clamped; must sit at the max, not wrap negative.
        Assert.Equal(short.MaxValue, buffer[0]);
        Assert.True(buffer[0] > 0);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(-0.8f)]
    public void SoftLimit_LeavesSamplesBelowTheKneeUntouched(float sample)
    {
        Assert.Equal(sample, AudioMixer.SoftLimit(sample));
    }

    [Fact]
    public void SoftLimit_StaysWithinFullScaleAndKeepsOrderAndSign()
    {
        float previous = AudioMixer.SoftLimit(0.8f);
        foreach (float input in new[] { 0.9f, 1.0f, 1.5f, 3.0f, 10.0f })
        {
            float limited = AudioMixer.SoftLimit(input);
            Assert.InRange(limited, previous, 1.0f);
            Assert.Equal(-limited, AudioMixer.SoftLimit(-input));
            previous = limited;
        }
    }

    [Fact]
    public void MasterVolume_ScalesTheMix()
    {
        var loud = new AudioMixer { MasterVolume = 1.0 };
        var quiet = new AudioMixer { MasterVolume = 0.2 };
        loud.Play(Tone(100, amplitude: 0.3f), gain: 1.0, pan: 0.0);
        quiet.Play(Tone(100, amplitude: 0.3f), gain: 1.0, pan: 0.0);

        var loudBuffer = new short[200];
        var quietBuffer = new short[200];
        loud.Render(loudBuffer, frameCount: 100);
        quiet.Render(quietBuffer, frameCount: 100);

        Assert.True(loudBuffer[0] > quietBuffer[0] * 3);
    }

    [Fact]
    public void Mixer_StealsTheLowestPriorityVoiceWhenFull()
    {
        var mixer = new AudioMixer();
        for (int i = 0; i < AudioMixer.MaxVoices; i++)
            mixer.Play(Tone(1000), gain: 1.0, pan: 0.0, priority: 0);
        Assert.Equal(AudioMixer.MaxVoices, mixer.ActiveVoiceCount);

        // One more voice at a higher priority must not grow the mixer past its cap.
        mixer.Play(Tone(1000), gain: 1.0, pan: 0.0, priority: 2);
        Assert.Equal(AudioMixer.MaxVoices, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void FinishedVoices_AreRemovedAfterRendering()
    {
        var mixer = new AudioMixer();
        mixer.Play(Tone(50), gain: 1.0, pan: 0.0);
        Assert.Equal(1, mixer.ActiveVoiceCount);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100); // longer than the voice's 50 frames

        Assert.Equal(0, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void Preview_PlaysWhilePaused()
    {
        var mixer = new AudioMixer { Paused = true };
        mixer.PlayPreview(Tone(100, 0.5f), gain: 1.0, pan: 0.0);

        var buffer = new short[200];
        Assert.Equal(1, mixer.ActivePreviewVoiceCount);
        mixer.Render(buffer, frameCount: 100);

        Assert.True(buffer[0] > 0);
    }

    [Fact]
    public void PausedRender_StillSilencesTheOtherVoices()
    {
        var mixer = new AudioMixer();
        mixer.Play(Tone(1000), gain: 1.0, pan: 0.0);
        mixer.PlayGuide(Tone(1000), gain: 1.0, pan: 0.0);
        mixer.Paused = true;

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.All(buffer, sample => Assert.Equal(0, sample));
        Assert.Equal(0, mixer.ActiveVoiceCount);
        Assert.Equal(0, mixer.ActiveGuideVoiceCount);
    }

    [Fact]
    public void NewPreview_ReplacesTheOneStillPlaying()
    {
        var mixer = new AudioMixer();
        mixer.PlayPreview(Tone(1000, 0.2f), gain: 1.0, pan: 0.0);
        mixer.PlayPreview(Tone(1000, 0.4f), gain: 1.0, pan: 0.0);

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.Equal(1, mixer.ActivePreviewVoiceCount);
        Assert.InRange(buffer[0], 0.4 * Math.Sqrt(0.5) * short.MaxValue - 5, 0.4 * Math.Sqrt(0.5) * short.MaxValue + 5); // the second alone, not a sum
    }

    [Fact]
    public void StopPreview_CutsItOff()
    {
        var mixer = new AudioMixer();
        mixer.PlayPreview(Tone(1000), gain: 1.0, pan: 0.0);
        mixer.StopPreview();

        var buffer = new short[200];
        mixer.Render(buffer, frameCount: 100);

        Assert.Equal(0, mixer.ActivePreviewVoiceCount);
        Assert.Equal(0, buffer[0]);
    }

    [Fact]
    public void Preview_FollowsMasterVolume()
    {
        var loud = new AudioMixer { MasterVolume = 1.0 };
        var quiet = new AudioMixer { MasterVolume = 0.5 };
        loud.PlayPreview(Tone(100, 0.4f), gain: 1.0, pan: 0.0);
        quiet.PlayPreview(Tone(100, 0.4f), gain: 1.0, pan: 0.0);

        var loudBuffer = new short[200];
        var quietBuffer = new short[200];
        loud.Render(loudBuffer, frameCount: 100);
        quiet.Render(quietBuffer, frameCount: 100);

        Assert.InRange(quietBuffer[0], loudBuffer[0] / 2 - 1, loudBuffer[0] / 2 + 1);
    }
}
