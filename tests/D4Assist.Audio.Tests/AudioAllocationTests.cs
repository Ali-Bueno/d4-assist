namespace D4Assist.Audio.Tests;

/// <summary>What the hot paths allocate: the audio thread renders 50 buffers a second, the radar plays up to four cues every
/// few hundred milliseconds and the NPC tap ticks every 25 ms.</summary>
public class AudioAllocationTests
{
    private sealed class FakeAudioOut : IAudioOut
    {
        public void Start(AudioRenderCallback render) { }
        public void Stop() { }
        public void Dispose() { }
    }

    private const int Frames = AudioFormat.SampleRate / 50; // one 20 ms buffer

    private static long AllocatedBy(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void A_delayed_cue_sounds_exactly_like_one_with_its_silence_written_in_front()
    {
        var cue = Enumerable.Range(0, 300).Select(i => MathF.Sin(i * 0.1f) * 0.5f).ToArray();
        const int silentFrames = 480; // 10 ms
        var delayed = new AudioMixer();
        var prepended = new AudioMixer();

        delayed.Play(cue, gain: 1, pan: 0.3, delaySeconds: (double)silentFrames / AudioFormat.SampleRate);
        prepended.Play([.. new float[silentFrames], .. cue], gain: 1, pan: 0.3);

        var a = new short[Frames * AudioFormat.Channels];
        var b = new short[Frames * AudioFormat.Channels];
        delayed.Render(a, Frames);
        prepended.Render(b, Frames);
        Assert.Equal(b, a);
        Assert.All(a.Take(silentFrames * AudioFormat.Channels), s => Assert.Equal(0, s));
        Assert.Contains(a, s => s != 0);
    }

    [Fact]
    public void Rendering_allocates_nothing_once_running()
    {
        var mixer = new AudioMixer();
        var buffer = new short[Frames * AudioFormat.Channels];
        mixer.Play(new float[AudioFormat.SampleRate], gain: 1, pan: 0); // a voice playing throughout
        mixer.Render(buffer, Frames);

        Assert.Equal(0, AllocatedBy(() => { for (int i = 0; i < 50; i++) mixer.Render(buffer, Frames); }));
    }

    [Fact]
    public void A_radar_cue_is_synthesized_once_not_on_every_play()
    {
        double now = 0;
        var mixer = new AudioMixer();
        using var player = new RadarCuePlayer(new FakeAudioOut(), mixer, () => now);
        player.Play(RadarCueKind.Blocked, RadarRay.Left, unexplored: false);
        mixer.Render(new short[Frames * AudioFormat.Channels], Frames);

        long bytes = AllocatedBy(() =>
        {
            for (int i = 0; i < 50; i++) { now += 1; player.Play(RadarCueKind.Blocked, RadarRay.Left, unexplored: false); }
        });

        Assert.True(bytes < 50 * 1024, $"{bytes} bytes for 50 cues"); // one cue's samples alone are several KB
    }

    [Fact]
    public void An_npc_tick_with_nobody_to_tap_allocates_nothing()
    {
        var player = new NpcCuePlayer(new AudioMixer(), () => 0, autoTick: false);
        player.Tick();

        Assert.Equal(0, AllocatedBy(() => { for (int i = 0; i < 40; i++) player.Tick(); }));
    }
}
