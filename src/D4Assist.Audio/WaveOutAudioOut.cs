using System.Runtime.InteropServices;

namespace D4Assist.Audio;

/// <summary>
/// WinMM waveOut* streaming sink: P/Invoke only, no native or NuGet audio library (the D2R radar itself
/// streams through the same Windows mixer, d2r-access src/wave_out.h). Triple-buffers on a dedicated
/// background thread so playing a cue never blocks the caller. Retries opening the device every few
/// seconds when none is present instead of failing, mirroring the D2R radar's own reopen loop
/// (d2r-access src/radar_audio.cpp:30, ReopenDelay).
/// </summary>
public sealed class WaveOutAudioOut : IAudioOut
{
    private const int BufferCount = 3; // triple buffering
    private const int BufferMs = 20;
    private static readonly TimeSpan ReopenDelay = TimeSpan.FromSeconds(2); // d2r-access radar_audio.cpp:30
    private static readonly int HeaderFlagsOffset = Marshal.OffsetOf<NativeWaveOut.WaveHdr>(nameof(NativeWaveOut.WaveHdr.dwFlags)).ToInt32();

    private readonly int _frameCount = AudioFormat.SampleRate * BufferMs / 1000;
    private readonly AutoResetEvent _bufferDone = new(false);
    private Thread? _thread;
    private volatile bool _running;
    private AudioRenderCallback? _render;
    private bool _disposed;

    public void Start(AudioRenderCallback render)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread != null) return;
        _render = render;
        _running = true;
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "D4Assist.Audio.WaveOut" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _bufferDone.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _bufferDone.Dispose();
    }

    private void RunLoop()
    {
        var buffers = new short[BufferCount][];
        var pins = new GCHandle[BufferCount];
        for (int i = 0; i < BufferCount; i++)
        {
            buffers[i] = new short[_frameCount * AudioFormat.Channels];
            pins[i] = GCHandle.Alloc(buffers[i], GCHandleType.Pinned);
        }
        var headers = new NativeWaveOut.WaveHdr[BufferCount];
        var headersHandle = GCHandle.Alloc(headers, GCHandleType.Pinned);
        IntPtr headersBase = headersHandle.AddrOfPinnedObject();
        int headerSize = Marshal.SizeOf<NativeWaveOut.WaveHdr>();

        try
        {
            while (_running)
            {
                IntPtr device = OpenDevice();
                if (device == IntPtr.Zero)
                {
                    _bufferDone.WaitOne(ReopenDelay); // no device present: retry later, stay silent meanwhile
                    continue;
                }

                PlayUntilStoppedOrFailed(device, headers, headersBase, headerSize, buffers, pins);

                NativeWaveOut.waveOutReset(device);
                for (int i = 0; i < BufferCount; i++)
                    NativeWaveOut.waveOutUnprepareHeader(device, HeaderPtr(headersBase, headerSize, i), (uint)headerSize);
                NativeWaveOut.waveOutClose(device);
            }
        }
        finally
        {
            headersHandle.Free();
            foreach (var pin in pins) pin.Free();
        }
    }

    private void PlayUntilStoppedOrFailed(IntPtr device, NativeWaveOut.WaveHdr[] headers, IntPtr headersBase,
        int headerSize, short[][] buffers, GCHandle[] pins)
    {
        for (int i = 0; i < BufferCount; i++)
        {
            headers[i].lpData = pins[i].AddrOfPinnedObject();
            headers[i].dwBufferLength = (uint)(buffers[i].Length * sizeof(short));
            NativeWaveOut.waveOutPrepareHeader(device, HeaderPtr(headersBase, headerSize, i), (uint)headerSize);
        }

        var written = new bool[BufferCount];
        int next = 0;
        while (_running)
        {
            IntPtr headerPtr = HeaderPtr(headersBase, headerSize, next);
            if (written[next] && !WaitForBufferDone(headerPtr)) break; // Stop() requested mid-wait

            _render?.Invoke(buffers[next], _frameCount);
            if (NativeWaveOut.waveOutWrite(device, headerPtr, (uint)headerSize) != 0) break; // device failed; reopen
            written[next] = true;
            next = (next + 1) % BufferCount;
        }
    }

    /// <summary>Polls a header's WHDR_DONE flag, sleeping between checks — the same wait shape as the
    /// D2R radar's own buffer wait (d2r-access src/wave_out.h, Await), generalized to a reusable ring of
    /// buffers instead of one fire-and-forget cue.</summary>
    private bool WaitForBufferDone(IntPtr headerPtr)
    {
        while (_running)
        {
            uint flags = unchecked((uint)Marshal.ReadInt32(headerPtr, HeaderFlagsOffset));
            if ((flags & NativeWaveOut.WhdrDone) != 0) return true;
            _bufferDone.WaitOne(20);
        }
        return false;
    }

    private static IntPtr HeaderPtr(IntPtr baseAddr, int headerSize, int index) => IntPtr.Add(baseAddr, index * headerSize);

    private IntPtr OpenDevice()
    {
        var format = new NativeWaveOut.WaveFormatEx
        {
            wFormatTag = NativeWaveOut.WaveFormatPcm,
            nChannels = (ushort)AudioFormat.Channels,
            nSamplesPerSec = (uint)AudioFormat.SampleRate,
            nBlockAlign = (ushort)(AudioFormat.Channels * sizeof(short)),
            nAvgBytesPerSec = (uint)(AudioFormat.SampleRate * AudioFormat.Channels * sizeof(short)),
            wBitsPerSample = sizeof(short) * 8,
        };
        int result = NativeWaveOut.waveOutOpen(out IntPtr device, NativeWaveOut.WaveMapper, ref format,
            _bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, NativeWaveOut.CallbackEvent);
        return result == 0 ? device : IntPtr.Zero; // no device present: degrade to silence, never throw
    }
}
