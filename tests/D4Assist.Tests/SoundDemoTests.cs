using D4Assist.Audio;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Tests;

public class SoundDemoTests
{
    private sealed class FakeRadar : IRadarCuePlayer
    {
        public List<(RadarCueKind Kind, RadarRay Ray, bool Unexplored)> Played { get; } = [];
        public void Play(RadarCueKind kind, RadarRay ray, bool unexplored) => Played.Add((kind, ray, unexplored));
    }

    private sealed class FakeSpeech : ISpeechSink
    {
        public List<(string Text, bool Interrupt)> Spoken { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Spoken.Add((text, interrupt));
        public void Dispose() { }
    }

    private static (SoundDemo Demo, FakeRadar Radar, FakeSpeech Speech) Create()
    {
        var radar = new FakeRadar();
        var speech = new FakeSpeech();
        var demo = new SoundDemo(speech, d => AppStrings.Spanish.SoundExamples[d - 1], radar, new AudioMixer(), null, 1.0, null);
        return (demo, radar, speech);
    }

    [Theory]
    [InlineData(1, RadarCueKind.Exit, false)]
    [InlineData(3, RadarCueKind.Blocked, false)]
    [InlineData(5, RadarCueKind.Exit, true)]
    public void Radar_examples_say_their_name_then_play_the_cue_in_the_four_directions(int digit, RadarCueKind kind, bool unexplored)
    {
        var (demo, radar, speech) = Create();

        demo.Run(digit);

        Assert.Equal([(AppStrings.Spanish.SoundExamples[digit - 1], true)], speech.Spoken);
        Assert.Equal(
            [(kind, RadarRay.Up, unexplored), (kind, RadarRay.Right, unexplored), (kind, RadarRay.Down, unexplored), (kind, RadarRay.Left, unexplored)],
            radar.Played);
    }

    [Fact]
    public void Every_example_has_a_name_in_both_languages()
    {
        Assert.Equal(SoundDemo.ExampleCount, AppStrings.Spanish.SoundExamples.Count);
        Assert.Equal(SoundDemo.ExampleCount, AppStrings.English.SoundExamples.Count);
    }
}
