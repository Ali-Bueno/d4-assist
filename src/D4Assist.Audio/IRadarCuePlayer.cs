namespace D4Assist.Audio;

/// <summary>Plays one radar cue in one direction. Kept engine-free (CLAUDE.md §6): the caller owns the
/// world/collision model and only says "this direction reads as this cue".</summary>
public interface IRadarCuePlayer
{
    /// <summary>Queues one cue; consecutive cues play one after another, never overlapping (CueScheduler).</summary>
    void Play(RadarCueKind kind, RadarRay ray, bool unexplored);
}
