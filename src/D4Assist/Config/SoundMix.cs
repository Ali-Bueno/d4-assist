using D4Assist.Audio;

namespace D4Assist.Config;

/// <summary>A cue's gain (0..1, over its designed level) and repeat rate (1 = its designed rate).</summary>
public readonly record struct CueLevel(double Gain, double Rate)
{
    public static readonly CueLevel Full = new(1, 1);
}

/// <summary>The gains and repeat rates the audio plays with, computed once from a <see cref="SoundSettings"/> snapshot;
/// immutable, so the poll and audio threads can read it while the settings window builds the next one.</summary>
public sealed class SoundMix
{
    // Volume percent → amplitude: squared, so each step sounds about as big as the next and 50 is roughly half as loud.
    private const double VolumeCurveExponent = 2;
    private const double Percent = 100.0;

    private readonly Dictionary<SoundCue, CueLevel> _levels;
    private readonly Dictionary<SoundGroup, CueLevel> _groupLevels;
    private readonly Dictionary<SoundCue, double> _directionGain;

    /// <summary>Master amplitude for the whole mixer (AudioMixer.MasterVolume).</summary>
    public double Master { get; }

    private SoundMix(
        double master, Dictionary<SoundCue, CueLevel> levels, Dictionary<SoundGroup, CueLevel> groupLevels, Dictionary<SoundCue, double> directionGain)
    {
        Master = master;
        _levels = levels;
        _groupLevels = groupLevels;
        _directionGain = directionGain;
    }

    public static SoundMix Default { get; } = From(new SoundSettings());

    public static SoundMix From(SoundSettings settings)
    {
        var groupLevels = Enum.GetValues<SoundGroup>().ToDictionary(g => g, g => Level(settings.Group(g)));
        var levels = new Dictionary<SoundCue, CueLevel>();
        var directionGain = new Dictionary<SoundCue, double>();
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            var group = groupLevels[SoundCatalog.GroupOf(cue)];
            var own = Level(settings.Cue(cue));
            levels[cue] = new CueLevel(group.Gain * own.Gain, group.Rate * own.Rate);
            if (SoundCatalog.IsRadarDirection(cue)) directionGain[cue] = own.Gain;
        }
        return new SoundMix(Amplitude(settings.MasterVolume), levels, groupLevels, directionGain);
    }

    /// <summary>The cue's gain and rate: its group's and its own switch, volume and speed combined (master excluded).</summary>
    public CueLevel Level(SoundCue cue) => _levels[cue];

    /// <summary>The group's own switch, volume and speed, before any cue's.</summary>
    public CueLevel GroupLevel(SoundGroup group) => _groupLevels[group];

    /// <summary>A radar sound's gain: the radar group, the sound kind and the side it comes from.</summary>
    public double RadarGain(RadarCueKind kind, RadarRay ray) =>
        _levels[SoundCatalog.RadarCue(kind)].Gain * _directionGain[SoundCatalog.RadarCue(ray)];

    /// <summary>Amplitude for a volume percentage.</summary>
    public static double Amplitude(int volume) =>
        Math.Pow(Math.Clamp(volume, CueSetting.MinVolume, CueSetting.MaxVolume) / Percent, VolumeCurveExponent);

    private static CueLevel Level(CueSetting setting) => new(
        setting.Enabled ? Amplitude(setting.Volume) : 0,
        Math.Clamp(setting.Speed, CueSetting.MinSpeed, CueSetting.MaxSpeed) / Percent);
}

/// <summary>The mix in force: the settings window publishes, the audio reads <see cref="Current"/> or follows <see cref="Changed"/>.</summary>
public sealed class LiveSoundMix(SoundMix initial)
{
    private volatile SoundMix _current = initial;

    public SoundMix Current => _current;

    /// <summary>Raised on the publishing thread after <see cref="Current"/> changes.</summary>
    public event Action<SoundMix>? Changed;

    public void Publish(SoundMix mix)
    {
        _current = mix;
        Changed?.Invoke(mix);
    }
}
