using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public static class EnergyForecaster
{
    private const double KelvinOffset = 273.15;

    public static int HeatingDemand(double? temp, double? wind, double? cloudCover, double heatingBase = 18)
    {
        var tempDeficit = temp is { } t
            ? Math.Clamp((heatingBase - t) / heatingBase * 100, 0, 100)
            : 50.0;
        var windChill = wind is { } w
            ? Math.Clamp(w / 10 * 100, 0, 100)
            : 50.0;
        var cloudCooling = cloudCover is { } c
            ? Math.Clamp(c, 0, 100)
            : 50.0;

        return (int)Math.Round(Math.Clamp(0.5 * tempDeficit + 0.3 * windChill + 0.2 * cloudCooling, 0, 100));
    }

    public static double? CopEstimate(double? outdoorTemp, double flowTemp = 35, double carnotEfficiency = 0.45)
    {
        if (outdoorTemp is not { } t) return null;
        if (t >= flowTemp) return null;

        var tHotK = flowTemp + KelvinOffset;
        var tColdK = t + KelvinOffset;
        return Math.Round(carnotEfficiency * tHotK / (tHotK - tColdK), 2);
    }

    public static IReadOnlyList<(int HoursFromNow, double Cop)> CopOptimalHours(
        ForecastSeries series, ParameterDef tempParam, double flowTemp, double carnotEfficiency,
        int count, DateTimeOffset now)
    {
        var cutoff = now.AddHours(24);
        var candidates = new List<(int HoursFromNow, double Cop)>();

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > cutoff) continue;
            var temp = point.Get(tempParam);
            var cop = CopEstimate(temp, flowTemp, carnotEfficiency);
            if (cop is not { } c) continue;

            var hours = (int)Math.Round((point.ValidAt - now).TotalHours);
            candidates.Add((hours, c));
        }

        return candidates.OrderByDescending(c => c.Cop).Take(count).ToList();
    }

    public static int ShadingScore(double? radiation, double? isDay, double? temp)
    {
        var radScore = radiation is { } r ? Math.Clamp(r / 1000 * 100, 0, 100) : 50.0;
        var dayScore = isDay is 1.0 ? 100.0 : 0.0;
        var overheatScore = temp is { } t && t > 25
            ? Math.Clamp((t - 25) / 15 * 100, 0, 100)
            : 0.0;

        return (int)Math.Round(Math.Clamp(0.5 * radScore + 0.1 * dayScore + 0.4 * overheatScore, 0, 100));
    }

    public static string BatteryStrategy(int solarYield, double? isDay)
    {
        var isDayTime = isDay is 1.0;
        if (solarYield > 60 && isDayTime) return "charge";
        if (!isDayTime || solarYield < 20) return "discharge";
        return "hold";
    }

    public static int NightCoolingPotential(
        ForecastSeries series, ParameterDef tempParam, ParameterDef humidityParam,
        ParameterDef windParam, ParameterDef rainProbParam, double indoorTemp, DateTimeOffset now)
    {
        var bestScore = 0;
        var searchEnd = now.AddHours(48);

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > searchEnd) continue;

            var hour = point.ValidAt.Hour;
            if (hour is >= 6 and < 22) continue;

            var temp = point.Get(tempParam);
            var humidity = point.Get(humidityParam);
            var wind = point.Get(windParam);
            var rainProb = point.Get(rainProbParam);

            var score = IndexScorer.Ventilation(temp, indoorTemp, humidity, wind, rainProb);
            if (score > bestScore) bestScore = score;
        }

        return bestScore;
    }
}
