using Njord.Messages.Indices;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class SolarScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Solar;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Daylight, m => Compute(m.Radiation, m.CloudCover, m.Temperature, preferences));

    public static int Compute(double? radiation, double? cloudCover, double? temp, ResolvedPreferences prefs)
    {
        var radScore = radiation is { } r ? Math.Clamp(r / 1000 * 100, 0, 100) : Neutral;
        var tempEff = temp is { } t and > 25
            ? Penalize(Math.Clamp(100 - (t - 25) * 4, 0, 100), prefs.HeatSensitivity)
            : 100.0;
        return Clamp(0.5 * radScore + 0.3 * CloudScore(cloudCover) + 0.2 * tempEff);
    }
}
