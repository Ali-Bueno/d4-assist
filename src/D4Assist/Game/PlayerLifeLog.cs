using System.Globalization;

namespace D4Assist.Game;

/// <summary>The player's life (ACD Hitpoints_Cur of Hitpoints_Max_Total).</summary>
public readonly record struct PlayerLife(float Current, float Max)
{
    public float Fraction => Max > 0 ? Math.Clamp(Current / Max, 0, 1) : 0;
}

/// <summary>
/// Debug log of the player's life: a line whenever it moves by a hundredth of the full life or more, with the change and where the
/// player stands, so a death can be laid against the HAZARD and TELEGRAPH lines around it (live 2026-10-23: four deaths to the
/// Horadric guardian with no warning, and nothing in the log said which hit killed).
/// </summary>
public sealed class PlayerLifeLog(Action<string> log)
{
    private const float StepFraction = 0.01f;

    private readonly object _gate = new();
    private PlayerState? _player;
    private float? _logged;

    public void OnPlayer(PlayerState player)
    {
        lock (_gate) _player = player;
    }

    public void OnLife(PlayerLife life)
    {
        lock (_gate)
        {
            float change = _logged is { } before ? life.Current - before : 0;
            if (_logged is not null && MathF.Abs(change) < life.Max * StepFraction && (life.Current > 0 || _logged == 0)) return;
            _logged = life.Current;
            string where = _player is { } p ? string.Create(CultureInfo.InvariantCulture, $" at ({p.X:0.0},{p.Y:0.0})") : "";
            string delta = change == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $" {change:+0;-0}");
            log(string.Create(CultureInfo.InvariantCulture, $"LIFE {life.Current:0}/{life.Max:0} ({life.Fraction:P0}){delta}{where}"));
        }
    }
}
