namespace D4Assist.Audio.Tests;

/// <summary>IRadarCuePlayer wiring: direction model → mixer, with a fake IAudioOut that never opens a
/// device (it just records the render callback so the test can pump it manually).</summary>
public class RadarCuePlayerTests
{
    private sealed class FakeAudioOut : IAudioOut
    {
        public void Start(AudioRenderCallback render) { }
        public void Stop() { }
        public void Dispose() { }
    }

    private static long ChannelEnergy(short[] buffer, int channel)
    {
        long sum = 0;
        for (int i = channel; i < buffer.Length; i += AudioFormat.Channels) sum += Math.Abs((int)buffer[i]);
        return sum;
    }

    [Fact]
    public void Play_PansLeftRayToTheLeftChannel()
    {
        var mixer = new AudioMixer();
        using var player = new RadarCuePlayer(new FakeAudioOut(), mixer) { Frame = RadarFrame.Cardinal };

        player.Play(RadarCueKind.Blocked, RadarRay.Left, unexplored: false);

        var buffer = new short[8192];
        mixer.Render(buffer, frameCount: 4096);

        // pan = -0.9 (D2R's PanWidth, not a full -1), so the ratio is sqrt(1.9/0.1) =~ 4.36, not 5+.
        Assert.True(ChannelEnergy(buffer, 0) > ChannelEnergy(buffer, 1) * 3);
    }

    [Fact]
    public void Volume_ScalesThePlayedCue()
    {
        var loudMixer = new AudioMixer();
        var quietMixer = new AudioMixer();
        using var loud = new RadarCuePlayer(new FakeAudioOut(), loudMixer) { Volume = 1.0 };
        using var quiet = new RadarCuePlayer(new FakeAudioOut(), quietMixer) { Volume = 0.1 };

        loud.Play(RadarCueKind.Exit, RadarRay.Up, unexplored: false);
        quiet.Play(RadarCueKind.Exit, RadarRay.Up, unexplored: false);

        var loudBuffer = new short[8192];
        var quietBuffer = new short[8192];
        loudMixer.Render(loudBuffer, frameCount: 4096);
        quietMixer.Render(quietBuffer, frameCount: 4096);

        Assert.True(ChannelEnergy(loudBuffer, 0) > ChannelEnergy(quietBuffer, 0) * 3);
    }

    [Fact]
    public void Dispose_StopsAndDisposesTheAudioOutOnlyOnce()
    {
        var stops = 0;
        var disposals = 0;
        var audioOut = new CountingAudioOut(() => stops++, () => disposals++);
        var player = new RadarCuePlayer(audioOut);

        player.Dispose();

        Assert.Equal(1, stops);
        Assert.Equal(1, disposals);
    }

    private sealed class CountingAudioOut(Action onStop, Action onDispose) : IAudioOut
    {
        public void Start(AudioRenderCallback render) { }
        public void Stop() => onStop();
        public void Dispose() => onDispose();
    }
}
