namespace D4Assist.Audio.Tests;

/// <summary>
/// Verifies the ported cue recipes (d2r-access src/radar_synth.cpp) without an audio device: length,
/// peak, dominant frequency and determinism.
/// </summary>
public class RadarCueSynthTests
{
    private const int SampleRate = AudioFormat.SampleRate;
    private static readonly int BaseCueFrames = (int)(0.120 * SampleRate); // Duration, radar_synth.cpp:49

    [Theory]
    [InlineData(RadarCueKind.Exit)]
    [InlineData(RadarCueKind.Blocked)]
    [InlineData(RadarCueKind.Door)]
    public void BaseCue_HasTheD2RDuration(RadarCueKind kind)
    {
        var samples = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);
        Assert.Equal(BaseCueFrames, samples.Length);
    }

    [Fact]
    public void Breath_IsLongerThanD2RSoItsSweepIsAudible()
    {
        const double breathSeconds = 0.180; // RadarCueSynth.BreathDuration (D4 change)
        var samples = RadarCueSynth.Synthesize(RadarCueKind.Open, vertical: 0, unexplored: false);
        Assert.Equal((int)(breathSeconds * SampleRate), samples.Length);
    }

    [Theory]
    [InlineData(RadarCueKind.Exit, 1100.0)]
    [InlineData(RadarCueKind.Blocked, 260.0)]
    [InlineData(RadarCueKind.Door, 180.0)]
    public void Tones_PeakAtTheirD2RFrequency(RadarCueKind kind, double expectedHz)
    {
        var samples = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);

        double atExpected = SignalAnalysis.GoertzelMagnitude(samples, expectedHz, SampleRate);
        double atUnrelated = SignalAnalysis.GoertzelMagnitude(samples, expectedHz * 2.3, SampleRate);

        Assert.True(atExpected > atUnrelated * 3,
            $"{kind}: expected {expectedHz} Hz to dominate ({atExpected:F1} vs {atUnrelated:F1}).");
    }

    [Theory]
    [InlineData(RadarCueKind.Exit, 1100.0)]
    [InlineData(RadarCueKind.Blocked, 260.0)]
    public void LowPitch_ShiftsToneDownByDownPitch(RadarCueKind kind, double baseHz)
    {
        const double downPitch = 0.86; // radar_synth.cpp:50
        var normal = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);
        var low = RadarCueSynth.Synthesize(kind, vertical: 1, unexplored: false);

        double lowAtShifted = SignalAnalysis.GoertzelMagnitude(low, baseHz * downPitch, SampleRate);
        double lowAtBase = SignalAnalysis.GoertzelMagnitude(low, baseHz, SampleRate);
        Assert.True(lowAtShifted > lowAtBase, $"{kind}: low-pitch cue should peak nearer {baseHz * downPitch} Hz.");

        double normalAtBase = SignalAnalysis.GoertzelMagnitude(normal, baseHz, SampleRate);
        Assert.True(normalAtBase > lowAtBase, $"{kind}: normal-pitch cue should read stronger at the base frequency than the low-pitch one.");
    }

    [Theory]
    [InlineData(RadarCueKind.Exit)]
    [InlineData(RadarCueKind.Blocked)]
    [InlineData(RadarCueKind.Door)]
    [InlineData(RadarCueKind.Open)]
    public void Peak_IsWithinExpectedRange(RadarCueKind kind)
    {
        var samples = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);
        float peak = SignalAnalysis.PeakAbs(samples);
        Assert.InRange(peak, 0.01f, 1.0f);
    }

    [Theory]
    [InlineData(RadarCueKind.Exit)]
    [InlineData(RadarCueKind.Open)]
    public void UnexploredTail_ExtendsPassableCuesOnly(RadarCueKind kind)
    {
        var plain = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);
        var marked = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: true);

        // Gap (1440) + two pips (1680 each) + inter-pip gap (1440), radar_synth.cpp:58-64.
        int expectedTailFrames = 30 * SampleRate / 1000 + 2 * (35 * SampleRate / 1000) + 30 * SampleRate / 1000;
        Assert.Equal(plain.Length + expectedTailFrames, marked.Length);

        // Some frame in the tail must carry sound (a pip), not just silence.
        var tail = marked[plain.Length..];
        Assert.True(SignalAnalysis.PeakAbs(tail) > 0.001f);
    }

    [Theory]
    [InlineData(RadarCueKind.Blocked)]
    [InlineData(RadarCueKind.Door)]
    public void UnexploredTail_NeverAppliesToObstacles(RadarCueKind kind)
    {
        var plain = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: false);
        var marked = RadarCueSynth.Synthesize(kind, vertical: 0, unexplored: true);
        Assert.Equal(plain.Length, marked.Length);
    }

    [Fact]
    public void Synthesis_IsDeterministic()
    {
        var first = RadarCueSynth.Synthesize(RadarCueKind.Open, vertical: 1, unexplored: true);
        var second = RadarCueSynth.Synthesize(RadarCueKind.Open, vertical: 1, unexplored: true);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Breath_RisesForUpAndFallsForDown()
    {
        var up = RadarCueSynth.Synthesize(RadarCueKind.Open, vertical: -1, unexplored: false);
        var down = RadarCueSynth.Synthesize(RadarCueKind.Open, vertical: 1, unexplored: false);
        int half = up.Length / 2;

        double UpperBandShare(float[] s, int from, int to)
        {
            var slice = s[from..to];
            double high = SignalAnalysis.GoertzelMagnitude(slice, 2000, SampleRate);
            double low = SignalAnalysis.GoertzelMagnitude(slice, 400, SampleRate);
            return high / (high + low);
        }

        Assert.True(UpperBandShare(up, half, up.Length) > UpperBandShare(up, 0, half), "Up breath should brighten over time.");
        Assert.True(UpperBandShare(down, half, down.Length) < UpperBandShare(down, 0, half), "Down breath should darken over time.");
        Assert.True(UpperBandShare(up, half, up.Length) > UpperBandShare(down, half, down.Length), "Up should end far brighter than down.");
    }
}
