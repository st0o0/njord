using Njord.Messages.Indices;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class LaundryScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Laundry;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.FullDay, m => Compute(
            m.Temperature, m.Humidity, m.Wind, m.RainProbability, m.SunshinePercent, preferences));

    public static int Compute(
        double? temp, double? humidity, double? wind, double? rainProb, double? sunshinePct, ResolvedPreferences prefs) =>
        Clamp(0.3 * TempScore(temp, prefs.HeatSensitivity) + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.2 * WindScore(wind, prefs.WindSensitivity) + 0.15 * RainScore(rainProb, prefs.RainSensitivity)
             + 0.1 * (sunshinePct ?? Neutral));
}
