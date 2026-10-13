using Njord.Compute.Contracts;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class IrrigationScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Irrigation;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.FullDay, m => Compute(
            m.RainProbability, m.Temperature, m.Humidity, m.Evapotranspiration, preferences));

    public static int Compute(double? rainProb, double? temp, double? humidity, double? et, ResolvedPreferences prefs)
    {
        var rainInverse = rainProb is { } r ? Math.Clamp(r / 60 * 100, 0, 100) : Neutral;
        var tempScore = temp is { } t ? Math.Clamp((t - 10) / 20 * 100, 0, 100) : Neutral;
        var humInverse = 100 - (humidity is { } h ? Math.Clamp((h - 40) / 50 * 100, 0, 100) : Neutral);
        var etScore = et is { } e ? Math.Clamp(e / 8 * 100, 0, 100) : Neutral;
        return Clamp(0.3 * (100 - rainInverse) + 0.25 * tempScore + 0.25 * humInverse + 0.2 * etScore);
    }
}
