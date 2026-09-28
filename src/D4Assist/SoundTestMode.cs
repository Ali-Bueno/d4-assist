using D4Assist.Audio;
using D4Assist.Speech;

namespace D4Assist;

/// <summary>
/// `D4Assist --sound-test`: speaks each radar cue's Spanish name, then plays it once per direction
/// (each direction announced first) so a blind user can learn the sounds without the game running. See
/// src/D4Assist.Audio/README.md for the cues themselves.
/// </summary>
internal static class SoundTestMode
{
    private const int PauseMs = 400;

    private static readonly (RadarCueKind Kind, string Name)[] Cues =
    [
        (RadarCueKind.Exit, "Salida"),
        (RadarCueKind.Open, "Paso abierto"),
        (RadarCueKind.Blocked, "Bloqueado"),
        (RadarCueKind.Door, "Puerta"),
    ];

    private static readonly (RadarRay Ray, string Name)[] Directions =
    [
        (RadarRay.Up, "Arriba"),
        (RadarRay.Right, "Derecha"),
        (RadarRay.Down, "Abajo"),
        (RadarRay.Left, "Izquierda"),
    ];

    public static int Run(bool noSpeech)
    {
        using ISpeechSink speech = noSpeech ? new ConsoleSpeechSink() : new PrismSpeechSink();
        using var player = new RadarCuePlayer(new WaveOutAudioOut());

        speech.Speak("Prueba de sonidos del radar", interrupt: true);
        Thread.Sleep(PauseMs);

        foreach (var (kind, name) in Cues)
        {
            speech.Speak(name, interrupt: true);
            Thread.Sleep(PauseMs);
            PlayEachDirection(speech, player, kind, unexplored: false);
        }

        speech.Speak("Sin explorar", interrupt: true);
        Thread.Sleep(PauseMs);
        PlayEachDirection(speech, player, RadarCueKind.Exit, unexplored: true);

        Thread.Sleep(PauseMs);
        return 0;
    }

    private static void PlayEachDirection(ISpeechSink speech, RadarCuePlayer player, RadarCueKind kind, bool unexplored)
    {
        foreach (var (ray, name) in Directions)
        {
            speech.Speak(name, interrupt: true);
            Thread.Sleep(PauseMs / 2);
            player.Play(kind, ray, unexplored);
            Thread.Sleep(PauseMs);
        }
    }
}
