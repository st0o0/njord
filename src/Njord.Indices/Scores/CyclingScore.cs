using Njord.Compute.Contracts;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class CyclingScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Cycling;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Daylight, m => Compute(m.Temperature, m.Humidity, m.Wind, m.RainProbability, preferences));

    public static int Compute(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.25 * RangeTempScore(temp, prefs.IdealTempLow, prefs.IdealTempHigh, prefs.HeatSensitivity)
             + 0.15 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.3 * Penalize(Math.Clamp(100 - (wind ?? 3) * 10, 0, 100), prefs.WindSensitivity)
             + 0.3 * RainScore(rainProb, prefs.RainSensitivity));
}
