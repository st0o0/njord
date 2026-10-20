using Njord.Forecast.Weather;

namespace Njord.Features.Alerts.Shared;

internal static class WeatherPointExtensions
{
    public static WeatherPoint? LowestBy(this IEnumerable<WeatherPoint> points, Func<WeatherPoint, double?> selector)
    {
        WeatherPoint? best = null;
        var bestValue = double.MaxValue;
        foreach (var point in points)
        {
            if (selector(point) is { } value && value < bestValue)
            {
                best = point;
                bestValue = value;
            }
        }

        return best;
    }

    public static WeatherPoint? HighestBy(this IEnumerable<WeatherPoint> points, Func<WeatherPoint, double?> selector)
    {
        WeatherPoint? best = null;
        var bestValue = double.MinValue;
        foreach (var point in points)
        {
            if (selector(point) is { } value && value > bestValue)
            {
                best = point;
                bestValue = value;
            }
        }

        return best;
    }
}
