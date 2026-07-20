using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public static class IndexScorer
{
    private static int Clamp(double value) => (int)Math.Round(Math.Clamp(value, 0, 100));

    private static double TempScore(double? temp) =>
        temp is not { } t ? 50 : Math.Clamp((t - 5) / 20 * 100, 0, 100);

    private static double HumidityScore(double? humidity) =>
        humidity is not { } h ? 50 : Math.Clamp((90 - h) / 50 * 100, 0, 100);

    private static double WindScore(double? wind) =>
        wind is not { } w ? 50 : Math.Clamp(w / 4 * 100, 0, 100);

    private static double RainScore(double? rainProb) =>
        rainProb is not { } r ? 50 : Math.Clamp((60 - r) / 60 * 100, 0, 100);

    private static double SunshineScore(double? sunshinePct) =>
        sunshinePct ?? 50;

    private static double CloudScore(double? cloudCover) =>
        cloudCover is not { } c ? 50 : Math.Clamp((100 - c), 0, 100);

    private static double TempComfort(double? temp)
    {
        if (temp is not { } t)
        {
            return 50;
        }

        var diff = Math.Abs(t - 22);
        return Math.Clamp(100 - diff * diff * 0.3, 0, 100);
    }

    private static double RunTempScore(double? temp)
    {
        if (temp is not { } t)
        {
            return 50;
        }

        if (t >= 5 && t <= 20)
        {
            var mid = 12.5;
            var diff = Math.Abs(t - mid);
            return Math.Clamp(100 - diff * diff * 0.5, 0, 100);
        }
        if (t < 5)
        {
            return Math.Clamp(100 - (5 - t) * 15, 0, 100);
        }

        return Math.Clamp(100 - (t - 20) * 5, 0, 100);
    }

    private static double BbqTempScore(double? temp) =>
        temp is not { } t ? 50 : Math.Clamp((t - 10) / 16 * 100, 0, 100);

    private static double BbqWindScore(double? wind)
    {
        if (wind is not { } w)
        {
            return 50;
        }

        if (w >= 1 && w <= 3)
        {
            return 100;
        }

        if (w < 1)
        {
            return 70;
        }

        return Math.Clamp(100 - (w - 3) * 12, 0, 100);
    }

    private static double BbqRainScore(double? rainProb) =>
        rainProb is not { } r ? 50 : Math.Clamp((30 - r) / 30 * 100, 0, 100);

    public static int LaundryDrying(double? temp, double? humidity, double? wind, double? rainProb, double? sunshinePct) =>
        Clamp(0.3 * TempScore(temp) + 0.25 * HumidityScore(humidity) + 0.2 * WindScore(wind)
             + 0.15 * RainScore(rainProb) + 0.1 * SunshineScore(sunshinePct));

    public static int OutdoorScore(double? temp, double? rainProb, double? wind, double? cloudCover) =>
        Clamp(0.35 * TempComfort(temp) + 0.25 * RainScore(rainProb)
             + 0.2 * Math.Clamp(100 - (wind ?? 5) * 8, 0, 100) + 0.2 * CloudScore(cloudCover));

    public static int RunningComfort(double? temp, double? humidity, double? wind, double? rainProb) =>
        Clamp(0.3 * RunTempScore(temp) + 0.25 * HumidityScore(humidity)
             + 0.2 * Math.Clamp(100 - (wind ?? 3) * 12, 0, 100) + 0.25 * RainScore(rainProb));

    public static int CyclingComfort(double? temp, double? humidity, double? wind, double? rainProb) =>
        Clamp(0.25 * RunTempScore(temp) + 0.15 * HumidityScore(humidity)
             + 0.3 * Math.Clamp(100 - (wind ?? 3) * 10, 0, 100) + 0.3 * RainScore(rainProb));

    public static int BbqWeather(double? temp, double? humidity, double? wind, double? rainProb) =>
        Clamp(0.3 * BbqTempScore(temp) + 0.1 * HumidityScore(humidity)
             + 0.25 * BbqWindScore(wind) + 0.35 * BbqRainScore(rainProb));

    public static int IrrigationNeed(double? rainProb, double? temp, double? humidity, double? et)
    {
        var rainInverse = rainProb is { } r ? Math.Clamp(r / 60 * 100, 0, 100) : 50;
        var tempScore = temp is { } t ? Math.Clamp((t - 10) / 20 * 100, 0, 100) : 50;
        var humInverse = humidity is { } h ? Math.Clamp((h - 40) / 50 * 100, 0, 100) : 50;
        humInverse = 100 - humInverse;
        var etScore = et is { } e ? Math.Clamp(e / 8 * 100, 0, 100) : 50;
        return Clamp(0.3 * (100 - rainInverse) + 0.25 * tempScore + 0.25 * humInverse + 0.2 * etScore);
    }

    public static double HeatingDegreeDays(double meanTemp, double baseTemp = 18) =>
        Math.Max(0, baseTemp - meanTemp);

    public static double CoolingDegreeDays(double meanTemp, double baseTemp = 24) =>
        Math.Max(0, meanTemp - baseTemp);

    public static int SolarYield(double? radiation, double? cloudCover, double? temp)
    {
        var radScore = radiation is { } r ? Math.Clamp(r / 1000 * 100, 0, 100) : 50;
        var cloudInverse = CloudScore(cloudCover);
        var tempEff = temp is { } t && t > 25 ? Math.Clamp(100 - (t - 25) * 4, 0, 100) : 100.0;
        return Clamp(0.5 * radScore + 0.3 * cloudInverse + 0.2 * tempEff);
    }

    public static int Ventilation(double? outdoorTemp, double indoorTemp, double? humidity, double? wind, double? rainProb)
    {
        var tempDelta = outdoorTemp is { } ot
            ? Math.Clamp((indoorTemp - ot) / 10 * 100, 0, 100)
            : 50.0;
        var humScore = humidity is { } h ? Math.Clamp((70 - h) / 30 * 100, 0, 100) : 50;
        double windScore;
        if (wind is { } w)
        {
            windScore = w >= 2 && w <= 5 ? 100 : w < 2 ? w / 2 * 100 : Math.Clamp(100 - (w - 5) * 15, 0, 100);
        }
        else
        {
            windScore = 50;
        }

        var rainSc = RainScore(rainProb);
        return Clamp(0.3 * tempDelta + 0.25 * humScore + 0.25 * windScore + 0.2 * rainSc);
    }

    public static FrostProtectionInfo? FrostProtection(
        IReadOnlyList<ForecastSeries> modelSeries, ParameterDef tempParam, DateTimeOffset now)
    {
        var cutoff = now.AddHours(48);
        int? firstFrostHours = null;
        var modelsWithFrost = 0;

        foreach (var series in modelSeries)
        {
            var hasFrost = false;
            foreach (var point in series.Points)
            {
                if (point.ValidAt < now || point.ValidAt > cutoff)
                {
                    continue;
                }

                var val = point.Get(tempParam);
                if (val is not { } v || v > 0)
                {
                    continue;
                }

                hasFrost = true;
                var hours = (int)Math.Round((point.ValidAt - now).TotalHours);
                if (firstFrostHours is null || hours < firstFrostHours)
                {
                    firstFrostHours = hours;
                }

                break;
            }
            if (hasFrost)
            {
                modelsWithFrost++;
            }
        }

        if (firstFrostHours is null)
        {
            return null;
        }

        var confidence = modelSeries.Count > 0 ? (double)modelsWithFrost / modelSeries.Count : 0;
        return new FrostProtectionInfo(firstFrostHours.Value, Math.Round(confidence, 2));
    }

    public static VpdInfo? VpdCategory(double? temp, double? humidity)
    {
        if (temp is not { } t || humidity is not { } rh)
        {
            return null;
        }

        var svp = 0.6108 * Math.Exp(17.27 * t / (t + 237.3));
        var vpd = svp * (1 - rh / 100);
        vpd = Math.Round(vpd, 2);

        var category = vpd switch
        {
            < 0.4 => "low",
            < 1.2 => "optimal",
            < 2.0 => "high",
            _ => "critical",
        };
        return new VpdInfo(category, vpd);
    }
}
