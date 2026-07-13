using Njord.Domain;

namespace Njord.Enrichment;

public static class DerivedComputer
{
    private static readonly double[] BeaufortUpperBounds =
        [0.5, 1.5, 3.3, 5.5, 7.9, 10.7, 13.8, 17.1, 20.7, 24.4, 28.4, 32.6];

    public static int? Beaufort(double? windSpeedMs)
    {
        if (windSpeedMs is not { } v) return null;

        for (var i = 0; i < BeaufortUpperBounds.Length; i++)
        {
            if (v < BeaufortUpperBounds[i]) return i;
        }
        return 12;
    }

    public static double? WindChill(double? tempC, double? windSpeedMs)
    {
        if (tempC is not { } t || windSpeedMs is not { } w) return null;

        var vKmh = w * 3.6;
        if (t > 10.0 || vKmh <= 4.8) return null;

        var vPow = Math.Pow(vKmh, 0.16);
        return Math.Round(13.12 + 0.6215 * t - 11.37 * vPow + 0.3965 * t * vPow, 1);
    }

    public static string? DewPointComfort(double? dewPointC)
    {
        if (dewPointC is not { } dp) return null;

        return dp switch
        {
            < 10.0 => "dry",
            < 16.0 => "comfortable",
            < 19.0 => "sticky",
            < 22.0 => "oppressive",
            _ => "dangerous",
        };
    }

    public static double? DiurnalAmplitude(
        ForecastSeries series, ParameterDef tempParam, DateTimeOffset now)
    {
        var cutoff = now.AddHours(24);
        double? min = null, max = null;
        var count = 0;

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > cutoff) continue;
            var val = point.Get(tempParam);
            if (val is not { } v) continue;
            count++;
            if (min is null || v < min) min = v;
            if (max is null || v > max) max = v;
        }

        return count < 2 ? null : max!.Value - min!.Value;
    }

    public static double? SunshinePercent(
        ForecastSeries series,
        ParameterDef sunshineDurationParam,
        ParameterDef isDayParam,
        DateTimeOffset now)
    {
        var cutoff = now.AddHours(24);
        double totalSunshineSec = 0;
        var daylightHours = 0;
        var hasSunshine = false;

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > cutoff) continue;

            var isDay = point.Get(isDayParam);
            if (isDay is 1.0) daylightHours++;

            var sunshine = point.Get(sunshineDurationParam);
            if (sunshine is { } s)
            {
                hasSunshine = true;
                totalSunshineSec += s;
            }
        }

        if (!hasSunshine || daylightHours == 0) return null;

        var daylightSec = daylightHours * 3600.0;
        return Math.Round(totalSunshineSec / daylightSec * 100.0, 1);
    }

    public static string? WmoDescription(int? weatherCode)
    {
        if (weatherCode is not { } code) return null;
        return WmoTable.GetValueOrDefault(code);
    }

    public static bool? InversionDetected(
        double? pressureMsl, double? surfacePressure, double? temp2m, double? dewPoint)
    {
        if (pressureMsl is not { } msl ||
            surfacePressure is not { } sp ||
            temp2m is not { } t ||
            dewPoint is not { } dp)
            return null;

        return (msl - sp) > 3.0 && (t - dp) < 3.0;
    }

    private static readonly Dictionary<int, string> WmoTable = new()
    {
        [0] = "Clear sky",
        [1] = "Mainly clear",
        [2] = "Partly cloudy",
        [3] = "Overcast",
        [45] = "Fog",
        [48] = "Depositing rime fog",
        [51] = "Drizzle: light",
        [53] = "Drizzle: moderate",
        [55] = "Drizzle: dense",
        [56] = "Freezing drizzle: light",
        [57] = "Freezing drizzle: dense",
        [61] = "Rain: slight",
        [63] = "Rain: moderate",
        [65] = "Rain: heavy",
        [66] = "Freezing rain: light",
        [67] = "Freezing rain: heavy",
        [71] = "Snow fall: slight",
        [73] = "Snow fall: moderate",
        [75] = "Snow fall: heavy",
        [77] = "Snow grains",
        [80] = "Rain showers: slight",
        [81] = "Rain showers: moderate",
        [82] = "Rain showers: violent",
        [85] = "Snow showers: slight",
        [86] = "Snow showers: heavy",
        [95] = "Thunderstorm: slight or moderate",
        [96] = "Thunderstorm with slight hail",
        [99] = "Thunderstorm with heavy hail",
    };
}
