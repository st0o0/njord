using Njord.Compute.Contracts;
using static Njord.Indices.Scores.ScoreFormulas;

namespace Njord.Indices.Scores;

public sealed class NightVentilationScore : IIndexScore
{
    public IndexKind Kind => IndexKind.NightVentilation;

    public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) =>
        slice.Score(DayPeriod.Night, m => Compute(m.Temperature, m.Humidity, m.Wind, m.RainProbability, preferences));

    public static int Compute(double? outdoorTemp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs)
    {
        var tempDelta = outdoorTemp is { } ot
            ? Math.Clamp((prefs.IndoorTemp - ot) / 10 * 100, 0, 100)
            : Neutral;
        var humRaw = humidity is { } h ? Math.Clamp((70 - h) / 30 * 100, 0, 100) : Neutral;
        var humScore = Penalize(humRaw, prefs.HumiditySensitivity);
        var windScore = wind is { } w ? Penalize(WindRaw(w), prefs.WindSensitivity) : Neutral;
        return Clamp(0.3 * tempDelta + 0.25 * humScore + 0.25 * windScore + 0.2 * RainScore(rainProb, prefs.RainSensitivity));
    }

    private static double WindRaw(double w) =>
        w is >= 2 and <= 5 ? 100 : w < 2 ? w / 2 * 100 : Math.Clamp(100 - (w - 5) * 15, 0, 100);
}
