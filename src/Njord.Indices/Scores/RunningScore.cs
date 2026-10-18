using Njord.Messages.Indices;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class RunningScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Running;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Daylight, m => Compute(m.Temperature, m.Humidity, m.Wind, m.RainProbability, preferences));

    public static int Compute(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.3 * RangeTempScore(temp, prefs.IdealTempLow, prefs.IdealTempHigh, prefs.HeatSensitivity)
             + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.2 * Penalize(Math.Clamp(100 - (wind ?? 3) * 12, 0, 100), prefs.WindSensitivity)
             + 0.25 * RainScore(rainProb, prefs.RainSensitivity));
}
