using Njord.Messages.Derived;
using Njord.Model;

namespace Njord.Derived;

public static class DerivedFormulas
{
    private static readonly double[] BeaufortUpperBounds =
        [0.5, 1.5, 3.3, 5.5, 7.9, 10.7, 13.8, 17.1, 20.7, 24.4, 28.4, 32.6];

    public static int? Beaufort(double? windSpeedMs)
    {
        if (windSpeedMs is not { } v)
        {
            return null;
        }

        for (var i = 0; i < BeaufortUpperBounds.Length; i++)
        {
            if (v < BeaufortUpperBounds[i])
            {
                return i;
            }
        }
        return 12;
    }

    public static double? WindChill(double? tempC, double? windSpeedMs)
    {
        if (tempC is not { } t || windSpeedMs is not { } w)
        {
            return null;
        }

        var vKmh = w * 3.6;
        if (t > 10.0 || vKmh <= 4.8)
        {
            return null;
        }

        var vPow = Math.Pow(vKmh, 0.16);
        return Math.Round(13.12 + 0.6215 * t - 11.37 * vPow + 0.3965 * t * vPow, 1);
    }

    public static DewPointComfort? Comfort(double? dewPointC)
    {
        if (dewPointC is not { } dp)
        {
            return null;
        }

        return dp switch
        {
            < 10.0 => DewPointComfort.Dry,
            < 16.0 => DewPointComfort.Comfortable,
            < 19.0 => DewPointComfort.Sticky,
            < 22.0 => DewPointComfort.Oppressive,
            _ => DewPointComfort.Dangerous,
        };
    }

    public static WeatherCondition? Condition(int? weatherCode) =>
        WeatherConditions.FromWmoCode(weatherCode);

    public static bool? InversionDetected(
        double? pressureMsl, double? surfacePressure, double? temp2m, double? dewPoint)
    {
        if (pressureMsl is not { } msl ||
            surfacePressure is not { } sp ||
            temp2m is not { } t ||
            dewPoint is not { } dp)
        {
            return null;
        }

        return msl - sp > 3.0 && t - dp < 3.0;
    }
}
