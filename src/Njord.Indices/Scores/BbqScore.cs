using Njord.Compute.Contracts;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class BbqScore : IIndexScore
{
    public IndexKind Kind => IndexKind.Bbq;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Daylight, m => Compute(m.Temperature, m.Humidity, m.Wind, m.RainProbability, preferences));

    public static int Compute(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.3 * BbqTemp(temp, prefs.MinTemp) + 0.1 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.25 * BbqWind(wind, prefs.IdealWindLow, prefs.IdealWindHigh)
             + 0.35 * BbqRain(rainProb, prefs.RainSensitivity));

    private static double BbqTemp(double? temp, double minTemp) =>
        temp is not { } t ? Neutral : Math.Clamp((t - minTemp) / 16 * 100, 0, 100);

    private static double BbqWind(double? wind, double idealLow, double idealHigh)
    {
        if (wind is not { } w)
        {
            return Neutral;
        }

        if (w >= idealLow && w <= idealHigh)
        {
            return 100;
        }

        if (w < idealLow)
        {
            return 70;
        }

        return Math.Clamp(100 - (w - idealHigh) * 12, 0, 100);
    }

    private static double BbqRain(double? rainProb, double sensitivity) =>
        rainProb is not { } r ? Neutral : Penalize(Math.Clamp((30 - r) / 30 * 100, 0, 100), sensitivity);
}
