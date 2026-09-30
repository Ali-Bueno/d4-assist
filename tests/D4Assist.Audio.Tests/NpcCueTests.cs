namespace D4Assist.Audio.Tests;

/// <summary>The friendly-NPC tap: ported D2R timbre, placement and cadence. Never opens a device.</summary>
public class NpcCueTests
{
    private sealed class Clock { public double Now; }

    private static (AudioMixer Mixer, NpcCuePlayer Npc, Clock Clock) Create()
    {
        var clock = new Clock();
        var mixer = new AudioMixer();
        return (mixer, new NpcCuePlayer(mixer, () => clock.Now, autoTick: false), clock);
    }

    private static (double Left, double Right) Listen(AudioMixer mixer)
    {
        var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
        mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
        double left = 0, right = 0;
        for (int i = 0; i < buffer.Length; i += 2) { left += Math.Abs(buffer[i]); right += Math.Abs(buffer[i + 1]); }
        return (left, right);
    }

    private static int ZeroCrossings(float[] samples)
    {
        int count = 0;
        for (int i = 1; i < samples.Length; i++) if ((samples[i - 1] < 0) != (samples[i] < 0)) count++;
        return count;
    }

    private static double Energy(float[] samples, int from, int to)
    {
        double sum = 0;
        for (int i = from; i < to; i++) sum += samples[i] * samples[i];
        return sum;
    }

    [Fact]
    public void Tap_is_84_ms_long_and_decays()
    {
        var tap = NpcCueSynth.Synthesize(1.0);

        Assert.Equal(84 * AudioFormat.SampleRate / 1000, tap.Length);
        Assert.True(SignalAnalysis.PeakAbs(tap) > 0.05f);
        Assert.True(Energy(tap, 0, tap.Length / 4) > 20 * Energy(tap, tap.Length / 2, tap.Length));
    }

    [Fact]
    public void Screen_down_lowers_the_pitch()
    {
        Assert.Equal(1.0, NpcCueSynth.Pitch(1), 9);
        Assert.Equal(NpcCueSynth.RearPitch, NpcCueSynth.Pitch(-1), 9);
        Assert.True(ZeroCrossings(NpcCueSynth.Synthesize(NpcCueSynth.Pitch(-1))) < ZeroCrossings(NpcCueSynth.Synthesize(NpcCueSynth.Pitch(1))));
    }

    [Fact]
    public void Interval_is_450_ms_at_the_edge_150_ms_at_contact_and_monotonic()
    {
        Assert.Equal(0.150, NpcCuePlayer.IntervalSeconds(0), 6);
        Assert.Equal(0.450, NpcCuePlayer.IntervalSeconds(NpcCuePlayer.DefaultRangeUnits), 6);
        Assert.Equal(0.300, NpcCuePlayer.IntervalSeconds(NpcCuePlayer.DefaultRangeUnits / 2), 6);
        double previous = 0;
        for (double distance = 0; distance <= NpcCuePlayer.DefaultRangeUnits; distance += 0.5)
        {
            double interval = NpcCuePlayer.IntervalSeconds(distance);
            Assert.True(interval >= previous);
            previous = interval;
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void Balance_follows_the_screen_direction(double right)
    {
        var (mixer, npc, _) = Create();
        npc.Aim(right, up: 0.1, distanceUnits: 5);
        npc.Tick();

        var (left, rightEnergy) = Listen(mixer);

        if (right > 0) Assert.True(rightEnergy > 3 * left); else Assert.True(left > 3 * rightEnergy);
    }

    [Fact]
    public void Silent_beyond_range_and_after_Silence()
    {
        var (mixer, npc, _) = Create();
        npc.Aim(0, 1, npc.RangeUnits + 1);
        npc.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);

        npc.Aim(0, 1, 5);
        npc.Silence();
        npc.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);
    }

    [Fact]
    public void Taps_repeat_at_the_contact_interval_and_stop_when_the_feed_goes_stale()
    {
        var (mixer, npc, clock) = Create();
        int plays = 0;
        var tick = new short[AudioFormat.SampleRate * NpcCuePlayer.TickMs / 1000 * AudioFormat.Channels];
        for (clock.Now = 0; clock.Now < 0.9; clock.Now += NpcCuePlayer.TickMs / 1000.0)
        {
            npc.Aim(0, 1, 0); // 150 ms
            int before = mixer.ActiveEntityVoiceCount;
            npc.Tick();
            if (mixer.ActiveEntityVoiceCount > before) plays++;
            mixer.Render(tick, tick.Length / AudioFormat.Channels);
        }
        Assert.InRange(plays, 5, 7);

        clock.Now += NpcCuePlayer.StaleAimSeconds + 0.5;
        npc.Tick();
        Assert.Equal((0, 0), Listen(mixer));
    }

    [Fact]
    public void Rate_divides_the_interval_between_taps()
    {
        var (slowMixer, slow, slowClock) = Create();
        var (fastMixer, fast, fastClock) = Create();
        fast.Rate = 2.0;
        slow.Aim(0, 1, NpcCuePlayer.DefaultRangeUnits); // 450 ms apart
        fast.Aim(0, 1, NpcCuePlayer.DefaultRangeUnits); // 225 ms apart
        slow.Tick();
        fast.Tick();
        Listen(slowMixer);
        Listen(fastMixer);

        slowClock.Now = fastClock.Now = 0.3;
        slow.Aim(0, 1, NpcCuePlayer.DefaultRangeUnits);
        fast.Aim(0, 1, NpcCuePlayer.DefaultRangeUnits);
        slow.Tick();
        fast.Tick();

        Assert.Equal(0, slowMixer.ActiveEntityVoiceCount);
        Assert.Equal(1, fastMixer.ActiveEntityVoiceCount);
    }

    [Fact]
    public void Zero_volume_plays_nothing()
    {
        var (mixer, npc, _) = Create();
        npc.Volume = 0;
        npc.Aim(0, 1, 5);
        npc.Tick();

        Assert.Equal(0, mixer.ActiveEntityVoiceCount);
    }
}
