using Njord.Messages.Indices;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class OutdoorScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Outdoor;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Daylight, m => Compute(
            m.Temperature, m.Humidity, m.RainProbability, m.Wind, m.CloudCover, preferences));

    public static int Compute(
        double? temp, double? humidity, double? rainProb, double? wind, double? cloudCover, ResolvedPreferences prefs) =>
        Clamp(0.25 * TempComfort(temp, prefs.IdealTemp, prefs.HeatSensitivity)
             + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.15 * RainScore(rainProb, prefs.RainSensitivity)
             + 0.20 * Breeze(wind, prefs.WindSensitivity)
             + 0.15 * CloudScore(cloudCover));

    public static double Breeze(double? wind, double sensitivity)
    {
        if (wind is not { } w)
        {
            return Neutral;
        }

        double raw;
        if (w is >= 2 and <= 4)
        {
            raw = 100;
        }
        else if (w < 2)
        {
            raw = 30 + w / 2 * 35;
        }
        else
        {
            raw = Math.Clamp(100 - (w - 4) * 12, 0, 100);
        }

        var penalty = 100 - raw;
        return Math.Clamp(100 - penalty * sensitivity, 0, 100);
    }

    private static double TempComfort(double? temp, double idealTemp, double sensitivity)
    {
        if (temp is not { } t)
        {
            return Neutral;
        }

        var diff = Math.Abs(t - idealTemp);
        var raw = Math.Clamp(100 - diff * diff * 0.55, 0, 100);
        return Penalize(raw, sensitivity);
    }
}
