using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public static class IndexScorer
{
    private static int Clamp(double value) => (int)Math.Round(Math.Clamp(value, 0, 100));

    private static double Penalize(double rawScore, double sensitivity) =>
        Math.Clamp(100 - (100 - rawScore) * sensitivity, 0, 100);

    private static double TempScore(double? temp, double sensitivity) =>
        temp is not { } t ? 50 : Penalize(Math.Clamp((t - 5) / 20 * 100, 0, 100), sensitivity);

    private static double HumidityScore(double? humidity, double sensitivity) =>
        humidity is not { } h ? 50 : Penalize(Math.Clamp((70 - h) / 40 * 100, 0, 100), sensitivity);

    private static double WindScore(double? wind, double sensitivity) =>
        wind is not { } w ? 50 : Penalize(Math.Clamp(w / 4 * 100, 0, 100), sensitivity);

    private static double RainScore(double? rainProb, double sensitivity) =>
        rainProb is not { } r ? 50 : Penalize(Math.Clamp((60 - r) / 60 * 100, 0, 100), sensitivity);

    private static double SunshineScore(double? sunshinePct) =>
        sunshinePct ?? 50;

    private static double CloudScore(double? cloudCover) =>
        cloudCover is not { } c ? 50 : Math.Clamp(100 - c, 0, 100);

    private static double TempComfort(double? temp, double idealTemp, double sensitivity)
    {
        if (temp is not { } t)
        {
            return 50;
        }

        var diff = Math.Abs(t - idealTemp);
        var raw = Math.Clamp(100 - diff * diff * 0.55, 0, 100);
        return Penalize(raw, sensitivity);
    }

    internal static double BreezeScore(double? wind, double sensitivity)
    {
        if (wind is not { } w)
        {
            return 50;
        }

        double raw;
        if (w >= 2 && w <= 4)
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

    private static double RunTempScore(double? temp, double low, double high, double sensitivity)
    {
        if (temp is not { } t)
        {
            return 50;
        }

        double raw;
        if (t >= low && t <= high)
        {
            var mid = (low + high) / 2;
            var diff = Math.Abs(t - mid);
            raw = Math.Clamp(100 - diff * diff * 0.5, 0, 100);
        }
        else if (t < low)
        {
            raw = Math.Clamp(100 - (low - t) * 15, 0, 100);
        }
        else
        {
            raw = Math.Clamp(100 - (t - high) * 5, 0, 100);
        }

        var penalty = 100 - raw;
        return Math.Clamp(100 - penalty * sensitivity, 0, 100);
    }

    private static double BbqTempScore(double? temp, double minTemp) =>
        temp is not { } t ? 50 : Math.Clamp((t - minTemp) / 16 * 100, 0, 100);

    private static double BbqWindScore(double? wind, double idealLow, double idealHigh)
    {
        if (wind is not { } w)
        {
            return 50;
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

    private static double BbqRainScore(double? rainProb, double sensitivity) =>
        rainProb is not { } r ? 50 : Penalize(Math.Clamp((30 - r) / 30 * 100, 0, 100), sensitivity);

    public static int LaundryDrying(double? temp, double? humidity, double? wind, double? rainProb, double? sunshinePct, ResolvedPreferences prefs) =>
        Clamp(0.3 * TempScore(temp, prefs.HeatSensitivity) + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.2 * WindScore(wind, prefs.WindSensitivity) + 0.15 * RainScore(rainProb, prefs.RainSensitivity)
             + 0.1 * SunshineScore(sunshinePct));

    public static int OutdoorScore(double? temp, double? humidity, double? rainProb, double? wind, double? cloudCover, ResolvedPreferences prefs) =>
        Clamp(0.25 * TempComfort(temp, prefs.IdealTemp, prefs.HeatSensitivity)
             + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.15 * RainScore(rainProb, prefs.RainSensitivity)
             + 0.20 * BreezeScore(wind, prefs.WindSensitivity)
             + 0.15 * CloudScore(cloudCover));

    public static int RunningComfort(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.3 * RunTempScore(temp, prefs.IdealTempLow, prefs.IdealTempHigh, prefs.HeatSensitivity)
             + 0.25 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.2 * Penalize(Math.Clamp(100 - (wind ?? 3) * 12, 0, 100), prefs.WindSensitivity)
             + 0.25 * RainScore(rainProb, prefs.RainSensitivity));

    public static int CyclingComfort(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.25 * RunTempScore(temp, prefs.IdealTempLow, prefs.IdealTempHigh, prefs.HeatSensitivity)
             + 0.15 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.3 * Penalize(Math.Clamp(100 - (wind ?? 3) * 10, 0, 100), prefs.WindSensitivity)
             + 0.3 * RainScore(rainProb, prefs.RainSensitivity));

    public static int BbqWeather(double? temp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs) =>
        Clamp(0.3 * BbqTempScore(temp, prefs.MinTemp) + 0.1 * HumidityScore(humidity, prefs.HumiditySensitivity)
             + 0.25 * BbqWindScore(wind, prefs.IdealWindLow, prefs.IdealWindHigh)
             + 0.35 * BbqRainScore(rainProb, prefs.RainSensitivity));

    public static int IrrigationNeed(double? rainProb, double? temp, double? humidity, double? et, ResolvedPreferences prefs)
    {
        var rainInverse = rainProb is { } r ? Math.Clamp(r / 60 * 100, 0, 100) : 50;
        var tempScore = temp is { } t ? Math.Clamp((t - 10) / 20 * 100, 0, 100) : 50;
        var humInverse = humidity is { } h ? Math.Clamp((h - 40) / 50 * 100, 0, 100) : 50;
        humInverse = 100 - humInverse;
        var etScore = et is { } e ? Math.Clamp(e / 8 * 100, 0, 100) : 50;
        return Clamp(0.3 * (100 - rainInverse) + 0.25 * tempScore + 0.25 * humInverse + 0.2 * etScore);
    }

    public static int SolarYield(double? radiation, double? cloudCover, double? temp, ResolvedPreferences prefs)
    {
        var radScore = radiation is { } r ? Math.Clamp(r / 1000 * 100, 0, 100) : 50;
        var cloudInverse = CloudScore(cloudCover);
        var tempEff = temp is { } t && t > 25
            ? Penalize(Math.Clamp(100 - (t - 25) * 4, 0, 100), prefs.HeatSensitivity)
            : 100.0;
        return Clamp(0.5 * radScore + 0.3 * cloudInverse + 0.2 * tempEff);
    }

    public static int NightVentilation(double? outdoorTemp, double? humidity, double? wind, double? rainProb, ResolvedPreferences prefs)
    {
        var tempDelta = outdoorTemp is { } ot
            ? Math.Clamp((prefs.IndoorTemp - ot) / 10 * 100, 0, 100)
            : 50.0;
        var humRaw = humidity is { } h ? Math.Clamp((70 - h) / 30 * 100, 0, 100) : 50;
        var humScore = Penalize(humRaw, prefs.HumiditySensitivity);
        double windScore;
        if (wind is { } w)
        {
            var windRaw = w >= 2 && w <= 5 ? 100 : w < 2 ? w / 2 * 100 : Math.Clamp(100 - (w - 5) * 15, 0, 100);
            windScore = Penalize(windRaw, prefs.WindSensitivity);
        }
        else
        {
            windScore = 50;
        }

        var rainSc = RainScore(rainProb, prefs.RainSensitivity);
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
