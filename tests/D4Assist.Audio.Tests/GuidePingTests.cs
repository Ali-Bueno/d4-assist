namespace D4Assist.Audio.Tests;

/// <summary>The exploration guide's ping: ported D2R cadence, placement, and its own voice budget. Never opens a device.</summary>
public class GuidePingTests
{
    private sealed class Clock { public double Now; }

    private static (AudioMixer Mixer, GuidePingPlayer Ping, Clock Clock) Create()
    {
        var clock = new Clock();
        var mixer = new AudioMixer();
        return (mixer, new GuidePingPlayer(mixer, () => clock.Now, autoTick: false), clock);
    }

    // Renders 100 ms and returns the summed |left| and |right| energy.
    private static (double Left, double Right) Listen(AudioMixer mixer)
    {
        var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
        mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
        double left = 0, right = 0;
        for (int i = 0; i < buffer.Length; i += 2) { left += Math.Abs(buffer[i]); right += Math.Abs(buffer[i + 1]); }
        return (left, right);
    }

    [Fact]
    public void Interval_runs_from_160_ms_up_close_to_1200_ms_at_the_far_distance()
    {
        Assert.Equal(0.160, GuidePingPlayer.IntervalSeconds(0), 6);
        Assert.Equal(1.200, GuidePingPlayer.IntervalSeconds(GuidePingPlayer.FarDistanceUnits), 6);
        Assert.Equal(1.200, GuidePingPlayer.IntervalSeconds(GuidePingPlayer.FarDistanceUnits * 5), 6);
        Assert.Equal(0.680, GuidePingPlayer.IntervalSeconds(GuidePingPlayer.FarDistanceUnits / 2), 6);
    }

    [Fact]
    public void Ping_stops_by_itself_when_the_feed_stops_re_aiming_it()
    {
        var (mixer, ping, clock) = Create();
        ping.Aim(right: 5, up: 0, remainingUnits: 100);
        ping.Tick();
        Listen(mixer);

        clock.Now = GuidePingPlayer.StaleAimSeconds + 0.5; // no Aim since: the game closed
        ping.Tick();

        Assert.Equal((0, 0), Listen(mixer));
    }

    [Fact]
    public void Arrival_chime_is_three_ping_notes_with_d2r_gaps()
    {
        int note = (int)(0.080 * AudioFormat.SampleRate), gap = (int)(0.060 * AudioFormat.SampleRate);

        Assert.Equal(3 * note + 2 * gap, GuidePingSynth.SynthesizeArrived().Length);
        Assert.Equal(note, GuidePingSynth.Synthesize(1.0).Length);
    }

    [Fact]
    public void Far_distance_is_the_d2r_forty_subtiles_in_d4_units()
    {
        Assert.Equal(40 * D2rScale.D4UnitsPerSubtile, GuidePingPlayer.FarDistanceUnits, 9);
    }

    [Fact]
    public void Pings_repeat_at_the_interval_for_the_remaining_route()
    {
        var (mixer, ping, clock) = Create();
        ping.Aim(right: 0, up: 1, remainingUnits: 0); // 160 ms

        var plays = 0;
        var tick = new short[AudioFormat.SampleRate * GuidePingPlayer.TickMs / 1000 * AudioFormat.Channels];
        for (clock.Now = 0; clock.Now < 1.0; clock.Now += GuidePingPlayer.TickMs / 1000.0)
        {
            int before = mixer.ActiveGuideVoiceCount;
            ping.Tick();
            if (mixer.ActiveGuideVoiceCount > before) plays++;
            mixer.Render(tick, tick.Length / AudioFormat.Channels);
        }

        Assert.InRange(plays, 5, 7); // ~1000/160
    }

    [Fact]
    public void Silence_stops_pings_and_aiming_resumes_them()
    {
        var (mixer, ping, clock) = Create();
        ping.Aim(0, 1, 100);
        ping.Tick();
        Listen(mixer); // let the first ping finish

        ping.Silence();
        clock.Now = 5;
        ping.Tick();
        Assert.Equal(0, mixer.ActiveGuideVoiceCount);

        ping.Aim(0, 1, 100);
        ping.Tick();
        Assert.Equal(1, mixer.ActiveGuideVoiceCount);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    public void Pan_follows_the_screen_side_of_the_waypoint(double right, double up)
    {
        var (mixer, ping, _) = Create();
        ping.Aim(right, up, 100);
        ping.Tick();

        var (left, rightEnergy) = Listen(mixer);

        if (right > 0) Assert.True(rightEnergy > left * 5, "waypoint on the right should sound on the right");
        else Assert.True(left > rightEnergy * 5, "waypoint on the left should sound on the left");
    }

    [Fact]
    public void Straight_ahead_is_centred()
    {
        var (mixer, ping, _) = Create();
        ping.Aim(0, 30, 100);
        ping.Tick();

        var (left, right) = Listen(mixer);

        Assert.Equal(left, right, 1);
    }

    [Fact]
    public void A_waypoint_behind_the_player_is_pitched_down_by_the_rear_ratio()
    {
        var ahead = GuidePingSynth.Synthesize(SpatialLoopPlayer.Pitch(1));
        var behind = GuidePingSynth.Synthesize(SpatialLoopPlayer.Pitch(-1));

        Assert.True(SignalAnalysis.GoertzelMagnitude(ahead, GuidePingSynth.PingHz, AudioFormat.SampleRate)
            > SignalAnalysis.GoertzelMagnitude(ahead, GuidePingSynth.PingHz * SpatialLoopPlayer.RearPitch, AudioFormat.SampleRate));
        Assert.True(SignalAnalysis.GoertzelMagnitude(behind, GuidePingSynth.PingHz * SpatialLoopPlayer.RearPitch, AudioFormat.SampleRate)
            > SignalAnalysis.GoertzelMagnitude(behind, GuidePingSynth.PingHz, AudioFormat.SampleRate));
    }

    [Fact]
    public void Ping_is_an_eighty_ms_note_peaking_at_the_d2r_level()
    {
        var samples = GuidePingSynth.Synthesize(1.0);

        Assert.Equal(AudioFormat.SampleRate * 80 / 1000, samples.Length);
        Assert.InRange(SignalAnalysis.PeakAbs(samples), GuidePingSynth.Level * 0.5, GuidePingSynth.Level * 1.2);
    }

    [Fact]
    public void Ping_neither_steals_from_nor_is_starved_by_radar_voices()
    {
        var (mixer, ping, _) = Create();
        for (int i = 0; i < AudioMixer.MaxVoices; i++) mixer.Play(new float[10_000], 1.0, 0.0, priority: 0);

        ping.Aim(0, 1, 100);
        ping.Tick();
        Assert.Equal(AudioMixer.MaxVoices, mixer.ActiveVoiceCount);
        Assert.Equal(1, mixer.ActiveGuideVoiceCount);

        mixer.Play(new float[10_000], 1.0, 0.0, priority: 5); // radar steals from radar only
        Assert.Equal(1, mixer.ActiveGuideVoiceCount);
    }

    [Fact]
    public void Volume_scales_the_ping()
    {
        var (loud, pingLoud, _) = Create();
        var (quiet, pingQuiet, _) = Create();
        pingQuiet.Volume = 0.25;
        pingLoud.Aim(0, 1, 100);
        pingQuiet.Aim(0, 1, 100);
        pingLoud.Tick();
        pingQuiet.Tick();

        Assert.Equal(1.0, Listen(quiet).Left / (Listen(loud).Left * 0.25), 2);
    }

    [Fact]
    public void Rate_divides_the_interval_between_pings()
    {
        var (slowMixer, slow, slowClock) = Create();
        var (fastMixer, fast, fastClock) = Create();
        fast.Rate = 2.0;
        slow.Aim(0, 1, GuidePingPlayer.FarDistanceUnits);
        fast.Aim(0, 1, GuidePingPlayer.FarDistanceUnits);

        slowClock.Now = 0; slow.Tick();
        fastClock.Now = 0; fast.Tick();
        slowClock.Now = 0.65; slow.Aim(0, 1, GuidePingPlayer.FarDistanceUnits); // due at 1.2 s
        fastClock.Now = 0.65; fast.Aim(0, 1, GuidePingPlayer.FarDistanceUnits); // due at 0.6 s

        Assert.True(Listen(slowMixer).Left > 0); // first tick's ping
        Assert.True(Listen(fastMixer).Left > 0);
        slow.Tick();
        fast.Tick();
        Assert.Equal(0, Listen(slowMixer).Left);
        Assert.True(Listen(fastMixer).Left > 0);
    }

    [Fact]
    public void Rate_at_or_below_zero_still_advances()
    {
        var (mixer, ping, clock) = Create();
        ping.Rate = 0;
        ping.Aim(0, 1, 0);
        ping.Tick();
        Listen(mixer);

        clock.Now = 0.9;
        ping.Aim(0, 1, 0);
        ping.Tick();
        Assert.Equal(0, Listen(mixer).Left); // guarded by a finite minimum, not an immediate repeat
    }

    [Fact]
    public void Zero_volume_plays_nothing_but_keeps_the_schedule()
    {
        var (mixer, ping, clock) = Create();
        ping.Volume = 0;
        ping.Aim(0, 1, 0);
        ping.Tick();
        Assert.Equal(0, mixer.ActiveGuideVoiceCount);

        ping.Volume = 1;
        clock.Now = 0.05; // inside the 160 ms interval the silent tick already started
        ping.Aim(0, 1, 0);
        ping.Tick();
        Assert.Equal(0, mixer.ActiveGuideVoiceCount);

        clock.Now = 0.2;
        ping.Aim(0, 1, 0);
        ping.Tick();
        Assert.Equal(1, mixer.ActiveGuideVoiceCount);
    }

    [Fact]
    public void ArrivalVolume_scales_only_the_arrival_chime()
    {
        var (full, pingFull, _) = Create();
        var (quiet, pingQuiet, _) = Create();
        pingQuiet.ArrivalVolume = 0.25;
        pingQuiet.Volume = 0; // the ping volume must not affect the chime
        pingFull.PlayArrived();
        pingQuiet.PlayArrived();

        Assert.Equal(1.0, Listen(quiet).Left / (Listen(full).Left * 0.25), 2);

        var (none, pingNone, _) = Create();
        pingNone.ArrivalVolume = 0;
        pingNone.PlayArrived();
        Assert.Equal(0, none.ActiveGuideVoiceCount);
    }
}
