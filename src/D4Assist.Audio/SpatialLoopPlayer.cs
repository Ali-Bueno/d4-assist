namespace D4Assist.Audio;

/// <summary>How one kind of loop source fades with distance: silent at <see cref="Reach"/>, <see cref="NearGain"/> at zero.</summary>
/// <param name="Falloff">Exponent on the remaining fraction of reach: 1 = linear fade, 2 = squared.</param>
public sealed record LoopProfile(double Reach, double NearGain, double Falloff);

/// <summary>A positioned loop source: stable key, sound id, offset to it in screen axes, and its profile.</summary>
/// <param name="Right">World units to the right of the listener on screen.</param>
/// <param name="Up">World units above the listener on screen (negative = below, i.e. behind).</param>
public readonly record struct LoopSource(string Key, string Sound, double Right, double Up, LoopProfile Profile);

/// <summary>Named loop profiles. Port of d2r-access ground-item loops, src/ground_items.h and ground_items.cpp.</summary>
public static class LoopProfiles
{
    // d2r-access ground_items.h:53 LoopVolume: near gain for peak-normalized loops.
    private const double D2rNearGain = 0.4;

    // d2r-access ground_items.h:34: quest loop reach, in subtiles.
    private const double QuestReachSubtiles = 45;

    /// <summary>D2R quest source: linear fade (ground_items.cpp:39, one-third left at 30 of 45), reach 45 subtiles
    /// (ground_items.h:34), converted with <see cref="D2rScale.D4UnitsPerSubtile"/>. Near gain is D2R's 0.4 times the radar's
    /// D4 loudness boost (combat drowned D2R levels, 2026-09-28), capped at full scale: min(1, 0.4 * 4) = 1.</summary>
    public static readonly LoopProfile Quest = new(
        Reach: QuestReachSubtiles * D2rScale.D4UnitsPerSubtile, NearGain: Math.Min(1.0, D2rNearGain * RadarCuePlayer.LoudnessBoost), Falloff: 1);

    /// <summary>User request (2026-09-28: "un audio beacon bien fuerte para el portal"): as loud as a quest source
    /// at close range, audible from more than twice as far. Linear fade like quest sources.</summary>
    public static readonly LoopProfile Beacon = new(Reach: 60, NearGain: 1.0, Falloff: 1);
}

/// <summary>
/// Drives looping sources on an <see cref="AudioMixer"/> from a per-poll source list: starts, retargets and
/// stops loop voices by key. Gain, pan and pitch follow d2r-access src/ground_items.cpp:Spatialize.
/// </summary>
public sealed class SpatialLoopPlayer
{
    /// <summary>Pitch of a source directly behind the listener (screen-down). d2r-access ground_items.h:53 RearPitch.</summary>
    public const double RearPitch = 0.94;

    // Rear pitch blends in over one unit of screen-down offset, avoiding chatter at the side axis (ground_items.cpp:41).
    private const double RearBlendUnits = 1.0;
    // Below this distance pan stops swinging with tiny offsets (a source right on top of the listener is centered).
    private const double MinPanDistance = 1.0;

    private readonly AudioMixer _mixer;
    private readonly IReadOnlyDictionary<string, float[]> _sounds;
    private readonly Dictionary<string, LoopVoice> _playing = [];

    /// <summary>User volume, 0..1; scales every loop.</summary>
    public double Volume { get; set; } = 1.0;

    public int ActiveCount => _playing.Count;

    public SpatialLoopPlayer(AudioMixer mixer, IReadOnlyDictionary<string, float[]> sounds)
    {
        _mixer = mixer;
        _sounds = sounds;
    }

    /// <summary>Loads every WAV in <paramref name="directory"/> by file name (no extension), peak-normalized like D2R's loops.</summary>
    public static Dictionary<string, float[]> LoadSounds(string directory)
    {
        var sounds = new Dictionary<string, float[]>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.wav"))
        {
            var samples = WavLoader.Load(path);
            WavLoader.Normalize(samples, LoopPeak);
            sounds[Path.GetFileNameWithoutExtension(path)] = samples;
        }
        return sounds;
    }

    // Recording peak before spatial gain, leaving headroom. d2r-access src/item_wave.h:11 LoopPeak.
    public const float LoopPeak = 0.9f;

    /// <summary>Distance fade: <c>NearGain * (1 - d/Reach)^Falloff</c>, silent at and beyond Reach (ground_items.cpp:38-39).</summary>
    public static double Gain(LoopProfile profile, double distance)
    {
        double proximity = Math.Clamp(1 - distance / profile.Reach, 0.0, 1.0);
        return profile.NearGain * Math.Pow(proximity, profile.Falloff);
    }

    /// <summary>Flat pan from the screen-right component, -1 (left) to 1 (right).</summary>
    public static double Pan(double right, double up) =>
        Math.Clamp(right / Math.Max(Math.Sqrt(right * right + up * up), MinPanDistance), -1.0, 1.0);

    /// <summary>1 in front or beside the listener, easing to <see cref="RearPitch"/> when the source is below on screen.</summary>
    public static double Pitch(double up) => 1 + (RearPitch - 1) * Math.Clamp(-up / RearBlendUnits, 0.0, 1.0);

    /// <summary>Applies this poll's sources: retargets known keys, starts new audible ones (nearest first), stops the rest.</summary>
    public void Update(IReadOnlyCollection<LoopSource> sources)
    {
        var live = new HashSet<string>();
        foreach (var source in sources.OrderBy(s => Math.Sqrt(s.Right * s.Right + s.Up * s.Up)))
        {
            double gain = Gain(source.Profile, Math.Sqrt(source.Right * source.Right + source.Up * source.Up)) * Volume;
            if (gain <= 0 || !_sounds.TryGetValue(source.Sound, out var samples)) continue;

            double pan = Pan(source.Right, source.Up), pitch = Pitch(source.Up);
            if (_playing.TryGetValue(source.Key, out var voice))
                voice.Set(gain, pan, pitch);
            else if (_mixer.StartLoop(samples, gain, pan, pitch) is { } started)
                _playing[source.Key] = started;
            else
                continue; // loop budget full; the nearer sources already hold it
            live.Add(source.Key);
        }

        foreach (var key in _playing.Keys.Where(key => !live.Contains(key)).ToList())
            Stop(key);
    }

    /// <summary>Stops every loop, e.g. when leaving the dungeon.</summary>
    public void StopAll()
    {
        foreach (var key in _playing.Keys.ToList()) Stop(key);
    }

    private void Stop(string key)
    {
        _playing[key].Stop();
        _playing.Remove(key);
    }
}
