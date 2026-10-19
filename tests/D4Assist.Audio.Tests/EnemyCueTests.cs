namespace D4Assist.Audio.Tests;

/// <summary>The enemy cue: ported D2R growl and class layers, cadence and the gap between starts. Never opens a device.</summary>
public class EnemyCueTests
{
    private sealed class Clock { public double Now; }

    private static (AudioMixer Mixer, EnemyCuePlayer Enemy, Clock Clock) Create()
    {
        var clock = new Clock();
        var mixer = new AudioMixer();
        return (mixer, new EnemyCuePlayer(mixer, () => clock.Now, autoTick: false), clock);
    }

    private static int ZeroCrossings(float[] samples)
    {
        int count = 0;
        for (int i = 1; i < samples.Length; i++) if ((samples[i - 1] < 0) != (samples[i] < 0)) count++;
        return count;
    }

    [Fact]
    public void Each_class_growls_90_ms_with_its_own_layer()
    {
        var boss = EnemyCueSynth.Synthesize(EnemyClass.Boss, 1.0);
        var elite = EnemyCueSynth.Synthesize(EnemyClass.Elite, 1.0);

        Assert.Equal((int)(EnemyCueSynth.CueMs * AudioFormat.SampleRate / 1000), boss.Length);
        Assert.Equal(boss.Length, elite.Length);
        Assert.Contains(boss, s => s != 0);
        Assert.NotEqual(boss, elite);
        Assert.All(boss.Concat(elite), s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void Behind_the_player_sounds_lower()
    {
        var front = EnemyCueSynth.Synthesize(EnemyClass.Boss, NpcCueSynth.Pitch(1));
        var behind = EnemyCueSynth.Synthesize(EnemyClass.Boss, NpcCueSynth.Pitch(-1));
        Assert.True(ZeroCrossings(behind) < ZeroCrossings(front));
    }

    [Fact]
    public void Interval_runs_from_150_ms_at_contact_to_600_ms_at_the_range_edge()
    {
        Assert.Equal(0.150, EnemyCuePlayer.IntervalSeconds(0), 6);
        Assert.Equal(0.600, EnemyCuePlayer.IntervalSeconds(EnemyCuePlayer.RangeUnits), 6);
        Assert.Equal(0.600, EnemyCuePlayer.IntervalSeconds(EnemyCuePlayer.RangeUnits * 2), 6);
    }

    [Fact]
    public void Two_enemies_due_together_start_at_least_100_ms_apart()
    {
        var (mixer, enemy, clock) = Create();
        enemy.Aim([new EnemyAim(1, EnemyClass.Boss, 0, 1, 0), new EnemyAim(2, EnemyClass.Elite, 1, 0, 0)]);

        enemy.Tick();
        Assert.Equal(1, mixer.ActiveEntityVoiceCount);
        clock.Now = 0.05;
        enemy.Tick();
        Assert.Equal(1, mixer.ActiveEntityVoiceCount);
        clock.Now = 0.1;
        enemy.Tick();
        Assert.Equal(2, mixer.ActiveEntityVoiceCount);
    }

    [Fact]
    public void An_enemy_beyond_reach_or_no_longer_aimed_is_silent()
    {
        var (mixer, enemy, clock) = Create();
        enemy.Aim([new EnemyAim(1, EnemyClass.Boss, 0, 1, EnemyCuePlayer.RangeUnits + 1)]);
        enemy.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);

        enemy.Aim([new EnemyAim(1, EnemyClass.Boss, 0, 1, 1)]);
        clock.Now += EnemyCuePlayer.StaleAimSeconds + 0.5;
        enemy.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);
    }

    [Fact]
    public void A_class_at_zero_volume_plays_nothing_while_the_other_does()
    {
        var (mixer, enemy, clock) = Create();
        enemy.SetLevel(EnemyClass.Elite, 0, 1);
        enemy.Aim([new EnemyAim(2, EnemyClass.Elite, 0, 1, 0)]);
        enemy.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);

        clock.Now = 1;
        enemy.Aim([new EnemyAim(1, EnemyClass.Boss, 0, 1, 0)]);
        enemy.Tick();
        Assert.Equal(1, mixer.ActiveEntityVoiceCount);
    }
}
