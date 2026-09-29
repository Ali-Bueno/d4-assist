using System.Text;

namespace D4Assist.Audio;

/// <summary>Reads a RIFF PCM16 mono WAV at <see cref="AudioFormat.SampleRate"/> into normalized floats.</summary>
public static class WavLoader
{
    private const int PcmFormatTag = 1;
    private const int BitsPerSample = 16;

    public static float[] Load(string path) => Parse(File.ReadAllBytes(path), path);

    /// <exception cref="InvalidDataException">Anything that is not PCM16 mono at the pipeline sample rate.</exception>
    public static float[] Parse(byte[] bytes, string name = "wav")
    {
        if (bytes.Length < 12 || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WAVE")
            throw new InvalidDataException($"{name}: not a RIFF/WAVE file.");

        bool haveFormat = false;
        float[]? samples = null;
        for (int at = 12; at + 8 <= bytes.Length;)
        {
            int size = BitConverter.ToInt32(bytes, at + 4);
            int start = at + 8;
            if (size < 0 || size > bytes.Length - start) throw new InvalidDataException($"{name}: truncated chunk.");

            switch (Tag(bytes, at))
            {
                case "fmt ":
                    if (size < 16) throw new InvalidDataException($"{name}: short fmt chunk.");
                    CheckFormat(bytes, start, name);
                    haveFormat = true;
                    break;
                case "data":
                    if (size % sizeof(short) != 0) throw new InvalidDataException($"{name}: odd data size.");
                    samples = new float[size / sizeof(short)];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = BitConverter.ToInt16(bytes, start + i * sizeof(short)) / 32768f;
                    break;
            }
            at = start + size + (size & 1); // chunks are word-aligned
        }

        if (!haveFormat || samples is null) throw new InvalidDataException($"{name}: missing fmt or data chunk.");
        return samples;
    }

    /// <summary>Scales <paramref name="samples"/> so the peak hits <paramref name="peak"/> (D2R normalizes each loop, item_wave.cpp:33-35).</summary>
    public static void Normalize(float[] samples, float peak)
    {
        float max = 0;
        foreach (var sample in samples) max = Math.Max(max, Math.Abs(sample));
        if (max == 0) return;
        float scale = peak / max;
        for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
    }

    private static void CheckFormat(byte[] bytes, int start, string name)
    {
        int tag = BitConverter.ToUInt16(bytes, start);
        int channels = BitConverter.ToUInt16(bytes, start + 2);
        int rate = BitConverter.ToInt32(bytes, start + 4);
        int bits = BitConverter.ToUInt16(bytes, start + 14);
        if (tag != PcmFormatTag || channels != 1 || bits != BitsPerSample || rate != AudioFormat.SampleRate)
            throw new InvalidDataException(
                $"{name}: expected PCM16 mono {AudioFormat.SampleRate} Hz, got tag {tag}, {channels} ch, {bits} bit, {rate} Hz.");
    }

    private static string Tag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);
}
