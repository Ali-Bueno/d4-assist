using D4Assist.Speech;
using Prismatoid;
using System.Reflection;
using Xunit;

namespace D4Assist.Tests;

public class PrismSpeechSinkTests
{
    [Fact]
    public void Under_Wine_only_the_bridged_backends_are_tried_highest_priority_first()
    {
        // PRISM v0.18.3's registry as seen on Windows: SAPI ties with Speech Dispatcher at 97, ahead in the registry.
        var registry = new[]
        {
            new PrismBackendInfo(0x89CC19C5C4AC1A56, "NVDA", 103, true),
            new PrismBackendInfo(0x1D6DF72422CEEE66, "SAPI", 97, true),
            new PrismBackendInfo(0x478B44F14AD3D89C, "Spiel", 96, true),
            new PrismBackendInfo(0xE3D6F895D949EBFE, "Speech Dispatcher", 97, true),
            new PrismBackendInfo(0x10AA1FC05A17F96C, "Orca", 100, true),
        };

        var tried = PrismSpeechSink.WineBridgesByPriority(registry).Select(b => b.Name);

        Assert.Equal(["Orca", "Speech Dispatcher", "Spiel"], tried);
    }
    [Fact]
    public void A_reader_whose_output_fails_is_spoken_to_instead_and_never_ends_the_app()
    {
        var reader = new FailingReader { FailOutput = true };
        var log = new List<string>();
        var sink = new PrismSpeechSink(reader, log: log.Add);

        sink.Output("12 gold", interrupt: true);
        sink.Output("13 gold", interrupt: true);

        Assert.Equal(["12 gold", "13 gold"], reader.Spoken);
        Assert.Equal(1, reader.OutputCalls); // output is not tried again once it failed
        Assert.Single(log);
    }

    [Fact]
    public void A_failed_speak_is_logged_once_and_not_thrown()
    {
        var log = new List<string>();
        var sink = new PrismSpeechSink(new FailingReader { FailSpeak = true }, log: log.Add);

        sink.Speak("a", interrupt: false);
        sink.Speak("b", interrupt: false);

        Assert.Single(log);
    }

    private sealed class FailingReader : IPrismBackend
    {
        public bool FailOutput, FailSpeak;
        public int OutputCalls;
        public List<string> Spoken = [];

        // PrismException's constructor is internal: the binding throws it from its native error codes only.
        private static PrismException Failure() => (PrismException)Activator.CreateInstance(typeof(PrismException),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [Enum.ToObject(typeof(PrismException).GetProperty("Error")!.PropertyType, 1)], null)!;

        public string Name => "ZDSR";
        public PrismBackendFeature Features => PrismBackendFeature.SupportsOutput | PrismBackendFeature.SupportsSpeak;
        public void Output(string text, bool interrupt)
        {
            OutputCalls++;
            if (FailOutput) throw Failure();
        }
        public void Speak(string text, bool interrupt)
        {
            if (FailSpeak) throw Failure();
            Spoken.Add(text);
        }
        public void Braille(string text) { }
        public float Volume { get; set; }
        public float Rate { get; set; }
        public float Pitch { get; set; }
        public bool IsSpeaking => false;
        public IEnumerable<PrismVoice> Voices => [];
        public int Channels => 0;
        public int SampleRate => 0;
        public PrismVoice? CurrentVoice { get; set; }
        public IEnumerable<ReadOnlyMemory<float>> SpeakToMemory(string text) => [];
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
        public void Dispose() { }
    }
}
