namespace D4Assist.Audio.Tests;

/// <summary>Small DSP helpers so tests can assert on frequency/peak without a full FFT.</summary>
internal static class SignalAnalysis
{
    /// <summary>Single-bin Goertzel magnitude at <paramref name="targetHz"/> — cheaper than an FFT when
    /// only a handful of candidate frequencies need comparing.</summary>
    public static double GoertzelMagnitude(float[] samples, double targetHz, int sampleRate)
    {
        double omega = 2 * Math.PI * targetHz / sampleRate;
        double coeff = 2 * Math.Cos(omega);
        double s1 = 0, s2 = 0;
        foreach (var sample in samples)
        {
            double s0 = sample + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        double real = s1 - s2 * Math.Cos(omega);
        double imag = s2 * Math.Sin(omega);
        return Math.Sqrt(real * real + imag * imag);
    }

    public static float PeakAbs(float[] samples)
    {
        float peak = 0;
        foreach (var sample in samples) peak = Math.Max(peak, Math.Abs(sample));
        return peak;
    }
}
