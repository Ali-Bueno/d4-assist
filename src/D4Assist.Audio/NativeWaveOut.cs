using System.Runtime.InteropServices;

namespace D4Assist.Audio;

/// <summary>
/// Thin P/Invoke surface for the WinMM waveOut* streaming API (winmm.dll, a system DLL — the D2R radar
/// uses the same mixer, d2r-access src/wave_out.h). Kept to exactly what WaveOutAudioOut needs.
/// </summary>
internal static class NativeWaveOut
{
    public const ushort WaveFormatPcm = 1;
    public const uint CallbackEvent = 0x00050000;
    public const uint WhdrDone = 0x00000001;
    public static readonly IntPtr WaveMapper = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    public struct WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WaveHdr
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")]
    public static extern int waveOutOpen(out IntPtr hWaveOut, IntPtr deviceId, ref WaveFormatEx format, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    public static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    public static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    public static extern int waveOutWrite(IntPtr hWaveOut, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    public static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    public static extern int waveOutClose(IntPtr hWaveOut);
}
