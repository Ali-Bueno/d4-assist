namespace D4Assist.Audio.Tests;

/// <summary>Repeat rate of loop voices (play heads, overlap-add) and CueRepeat. Never opens a device.</summary>
public class LoopRateTests
{
    private const int SoundFrames = 4800; // 100 ms; divides the one-second settle so renders stay period-aligned
    private const int SettleFrames = AudioFormat.SampleRate;

    private static float[] Sound() => Enumerable.Repeat(0.5f, SoundFrames).ToArray();

    // Starts a centred loop and renders one second so the 80 ms gain smoothing has settled.
    private static (AudioMixer Mixer, LoopVoice Loop) Settled(double rate)
    {
        var mixer = new AudioMixer();
        var loop = mixer.StartLoop(Sound(), gain: 1.0, pan: 0.0, rate: rate)!;
        Render(mixer, SettleFrames);
        return (mixer, loop);
    }

    // Left channel of the next frames.
    private static short[] Render(AudioMixer mixer, int frames)
    {
        var buffer = new short[frames * AudioFormat.Channels];
        mixer.Render(buffer, frames);
        return buffer.Where((_, i) => i % AudioFormat.Channels == 0).ToArray();
    }

    [Fact]
    public void Rate1_loops_back_to_back_without_a_gap()
    {
        var (mixer, _) = Settled(1.0);

        var left = Render(mixer, SoundFrames * 3);

        Assert.All(left, sample => Assert.True(sample > 0));
        Assert.True(left.Max() - left.Min() < 100); // constant level across the joins
    }

    [Fact]
    public void Rate_half_leaves_an_equal_silence_after_each_repetition()
    {
        var (mixer, _) = Settled(0.5);

        var left = Render(mixer, SoundFrames * 4);

        for (int period = 0; period < 2; period++)
        {
            int start = period * SoundFrames * 2;
            Assert.True(left[start + SoundFrames / 2] > 0);
            Assert.All(left.Skip(start + SoundFrames + 10).Take(SoundFrames - 20), sample => Assert.Equal(0, sample));
        }
    }

    [Fact]
    public void Rate2_overlaps_two_repetitions()
    {
        var (singleMixer, _) = Settled(1.0);
        var (mixer, _) = Settled(2.0);

        var single = Render(singleMixer, SoundFrames);
        var doubled = Render(mixer, SoundFrames * 2);

        Assert.All(doubled.Skip(10), sample => Assert.InRange(sample, single[0] * 1.9, single[0] * 2.1));
    }

    [Fact]
    public void Rate_is_clamped_to_the_maximum()
    {
        var (clamped, _) = Settled(1000);
        var (max, _) = Settled(LoopVoice.MaxRate);

        Assert.Equal(Render(max, SoundFrames * 3), Render(clamped, SoundFrames * 3));
    }

    [Fact]
    public void Rate_change_takes_effect_from_the_next_repetition()
    {
        var (mixer, loop) = Settled(1.0);
        Render(mixer, SoundFrames / 2);

        loop.Set(gain: 1.0, pan: 0.0, pitch: 1.0, rate: 0.5);
        var left = Render(mixer, SoundFrames * 4);

        Assert.Contains(left, sample => sample > 0);
        Assert.Contains(left.Skip(SoundFrames), sample => sample == 0); // silence appears once the new rate applies
    }

    [Fact]
    public void Stopping_still_fades_out()
    {
        var (mixer, loop) = Settled(2.0);
        loop.Stop();

        Render(mixer, SettleFrames);

        Assert.Equal(0, mixer.ActiveLoopCount);
    }

    [Fact]
    public void CueRepeat_places_each_copy_one_period_apart_and_sums_overlaps()
    {
        var sound = Enumerable.Repeat(1f, 100).ToArray();
        double period = 60.0 / AudioFormat.SampleRate;

        var output = CueRepeat.Render(sound, period, count: 3);

        Assert.Equal(2 * 60 + 100, output.Length);
        Assert.Equal(1f, output[0]);
        Assert.Equal(1f, output[59]);
        Assert.Equal(2f, output[60]);
        Assert.Equal(1f, output[100]);
        Assert.Equal(2f, output[120]);
        Assert.Equal(1f, output[^1]);
    }

    [Fact]
    public void CueRepeat_with_no_copies_is_empty() =>
        Assert.Empty(CueRepeat.Render([1f], 0.1, count: 0));
}
