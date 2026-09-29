namespace D4Assist.Audio.Tests;

/// <summary>WAV loading, the ported D2R loop curve, and loop voice management. Never opens a device.</summary>
public class SpatialLoopTests
{
    private const string Sound = "tone";
    private static readonly LoopProfile Linear = new(Reach: 10, NearGain: 0.5, Falloff: 1);

    private static byte[] Wav(short[] samples, int channels = 1, int rate = AudioFormat.SampleRate, int bits = 16, int tag = 1)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        int dataBytes = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)tag); w.Write((short)channels); w.Write(rate);
        w.Write(rate * channels * bits / 8); w.Write((short)(channels * bits / 8)); w.Write((short)bits);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        return stream.ToArray();
    }

    private static (AudioMixer Mixer, SpatialLoopPlayer Player) Create()
    {
        var mixer = new AudioMixer();
        var sounds = new Dictionary<string, float[]> { [Sound] = Enumerable.Repeat(0.5f, 480).ToArray() };
        return (mixer, new SpatialLoopPlayer(mixer, sounds));
    }

    // Renders one second (settles the 80 ms smoothing and finishes a stop fade); returns the last frame's (left, right).
    private static (short Left, short Right) Settle(AudioMixer mixer)
    {
        var buffer = new short[AudioFormat.SampleRate * AudioFormat.Channels];
        mixer.Render(buffer, AudioFormat.SampleRate);
        return (buffer[^2], buffer[^1]);
    }

    [Fact]
    public void WavLoader_ReadsPcm16Mono()
    {
        var samples = WavLoader.Parse(Wav([0, 16384, -32768]));
        Assert.Equal([0f, 0.5f, -1f], samples);
    }

    [Theory]
    [InlineData(2, AudioFormat.SampleRate, 16, 1)]
    [InlineData(1, 44100, 16, 1)]
    [InlineData(1, AudioFormat.SampleRate, 8, 1)]
    [InlineData(1, AudioFormat.SampleRate, 16, 3)]
    public void WavLoader_RejectsOtherFormats(int channels, int rate, int bits, int tag) =>
        Assert.Throws<InvalidDataException>(() => WavLoader.Parse(Wav([1, 2], channels, rate, bits, tag)));

    [Fact]
    public void WavLoader_RejectsGarbage() =>
        Assert.Throws<InvalidDataException>(() => WavLoader.Parse(new byte[40]));

    [Fact]
    public void ShippedQuestSound_IsTrimmedToSevenHundredMilliseconds()
    {
        var samples = WavLoader.Load(Path.Combine(AppContext.BaseDirectory, "Sounds", "quest-item.wav"));
        Assert.Equal(AudioFormat.SampleRate * 7 / 10, samples.Length);
    }

    [Fact]
    public void Gain_IsSilentAtAndPastReach_AndFallsMonotonically()
    {
        Assert.Equal(0.5, SpatialLoopPlayer.Gain(Linear, 0), 6);
        Assert.Equal(0, SpatialLoopPlayer.Gain(Linear, 10));
        Assert.Equal(0, SpatialLoopPlayer.Gain(Linear, 50));
        double previous = double.MaxValue;
        for (double d = 0; d <= 10; d += 0.5)
        {
            double gain = SpatialLoopPlayer.Gain(Linear, d);
            Assert.True(gain <= previous);
            previous = gain;
        }
    }

    [Fact]
    public void QuestProfile_KeepsOneThirdOfNearGainAtTwoThirdsOfReach() // D2R: 30 of 45 subtiles
    {
        var quest = LoopProfiles.Quest;
        Assert.Equal(quest.NearGain / 3, SpatialLoopPlayer.Gain(quest, quest.Reach * 2 / 3), 6);
    }

    [Fact]
    public void QuestProfile_NearGainIsD2rGainTimesTheRadarBoostCappedAtFullScale()
    {
        Assert.Equal(Math.Min(1.0, 0.4 * RadarCuePlayer.LoudnessBoost), LoopProfiles.Quest.NearGain);
    }

    [Fact]
    public void BeaconProfile_IsNoQuieterAndReachesFartherThanQuest()
    {
        Assert.True(LoopProfiles.Beacon.NearGain >= LoopProfiles.Quest.NearGain);
        Assert.True(LoopProfiles.Beacon.Reach > LoopProfiles.Quest.Reach);
    }

    [Fact]
    public void Pan_FollowsScreenRightComponent()
    {
        Assert.True(SpatialLoopPlayer.Pan(5, 0) > 0.9);
        Assert.True(SpatialLoopPlayer.Pan(-5, 0) < -0.9);
        Assert.Equal(0, SpatialLoopPlayer.Pan(0, 5), 6);
    }

    [Fact]
    public void Pitch_DropsOnlyWhenSourceIsBelowOnScreen()
    {
        Assert.Equal(1, SpatialLoopPlayer.Pitch(4));
        Assert.Equal(1, SpatialLoopPlayer.Pitch(0));
        Assert.Equal(SpatialLoopPlayer.RearPitch, SpatialLoopPlayer.Pitch(-4), 6);
        Assert.InRange(SpatialLoopPlayer.Pitch(-0.5), SpatialLoopPlayer.RearPitch, 1);
    }

    [Fact]
    public void RenderedLoop_PansTowardTheSourceSide()
    {
        var (mixer, player) = Create();
        player.Update([new LoopSource("a", Sound, Right: -4, Up: 0, Linear)]);
        var (left, right) = Settle(mixer);
        Assert.True(left > right * 4);

        var (mixerR, playerR) = Create();
        playerR.Update([new LoopSource("a", Sound, Right: 4, Up: 0, Linear)]);
        var (l2, r2) = Settle(mixerR);
        Assert.True(r2 > l2 * 4);
    }

    [Fact]
    public void Update_StartsRetargetsAndStopsByKey()
    {
        var (mixer, player) = Create();
        var a = new LoopSource("a", Sound, 1, 0, Linear);
        var b = new LoopSource("b", Sound, 2, 0, Linear);

        player.Update([a, b]);
        Assert.Equal(2, mixer.ActiveLoopCount);

        player.Update([a with { Right = 3 }, b]); // same keys: no new voices
        Assert.Equal(2, mixer.ActiveLoopCount);

        player.Update([a]); // b vanished
        Settle(mixer);
        Assert.Equal(1, player.ActiveCount);
        Assert.Equal(1, mixer.ActiveLoopCount);

        player.StopAll();
        Settle(mixer);
        Assert.Equal(0, mixer.ActiveLoopCount);
    }

    [Fact]
    public void SourceBeyondReach_IsNeverStarted_AndStopsWhenItDrifts()
    {
        var (mixer, player) = Create();
        player.Update([new LoopSource("a", Sound, 50, 0, Linear)]);
        Assert.Equal(0, mixer.ActiveLoopCount);

        player.Update([new LoopSource("a", Sound, 1, 0, Linear)]);
        Assert.Equal(1, mixer.ActiveLoopCount);
        player.Update([new LoopSource("a", Sound, 50, 0, Linear)]);
        Settle(mixer);
        Assert.Equal(0, mixer.ActiveLoopCount);
    }

    [Fact]
    public void LoopBudget_KeepsTheNearestSources()
    {
        var (mixer, player) = Create();
        var sources = Enumerable.Range(0, AudioMixer.MaxLoopVoices + 3)
            .Select(i => new LoopSource($"s{i}", Sound, 0.5 + i * 0.1, 0, Linear)).Reverse().ToList();
        player.Update(sources);
        Assert.Equal(AudioMixer.MaxLoopVoices, mixer.ActiveLoopCount);
        Assert.Equal(AudioMixer.MaxLoopVoices, player.ActiveCount);
    }

    [Fact]
    public void Loops_AndRadarCues_DoNotStealFromEachOther()
    {
        var (mixer, player) = Create();
        player.Update(Enumerable.Range(0, AudioMixer.MaxLoopVoices)
            .Select(i => new LoopSource($"s{i}", Sound, 1, 0, Linear)).ToList());

        for (int i = 0; i < AudioMixer.MaxVoices + 4; i++) mixer.Play(new float[1000], 1, 0, priority: 1);

        Assert.Equal(AudioMixer.MaxLoopVoices, mixer.ActiveLoopCount);
        Assert.Equal(AudioMixer.MaxVoices, mixer.ActiveVoiceCount);
        Assert.Null(mixer.StartLoop(new float[10], 1, 0)); // a full loop budget refuses instead of stealing
        Assert.Equal(AudioMixer.MaxVoices, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void LoopingSound_WrapsAroundAtItsEnd()
    {
        var mixer = new AudioMixer();
        mixer.StartLoop(Enumerable.Repeat(0.5f, 100).ToArray(), gain: 1, pan: 0);
        Settle(mixer); // 1 s = 48000 frames = many wraps; still sounding at the end
        var buffer = new short[2000];
        mixer.Render(buffer, 1000);
        Assert.True(buffer[^1] > 0);
    }
}
