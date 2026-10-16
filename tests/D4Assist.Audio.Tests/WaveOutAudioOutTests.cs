using D4Assist.Audio;

namespace D4Assist.Audio.Tests;

public class WaveOutAudioOutTests
{
    [Fact]
    public void RenderOrSilence_fills_the_buffer_through_the_render()
    {
        var samples = new short[4];

        var failure = WaveOutAudioOut.RenderOrSilence((buffer, _) => Array.Fill(buffer, (short)7), samples, 2);

        Assert.Null(failure);
        Assert.All(samples, s => Assert.Equal(7, s));
    }

    [Fact]
    public void RenderOrSilence_a_render_that_throws_plays_silence_instead_of_escaping()
    {
        var samples = new short[4];

        var failure = WaveOutAudioOut.RenderOrSilence((buffer, _) =>
        {
            buffer[0] = 99; // half-written when it fails
            throw new IndexOutOfRangeException();
        }, samples, 2);

        Assert.IsType<IndexOutOfRangeException>(failure);
        Assert.All(samples, s => Assert.Equal(0, s));
    }

    [Fact]
    public void Dispose_without_start_or_twice_is_harmless()
    {
        var output = new WaveOutAudioOut();
        output.Dispose();
        output.Dispose();
    }
}
