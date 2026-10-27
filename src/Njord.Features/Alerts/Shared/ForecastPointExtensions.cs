using Njord.Forecast.Forecast;

namespace Njord.Features.Alerts.Shared;

internal static class ForecastPointExtensions
{
    public static ForecastPoint? LowestBy(this IEnumerable<ForecastPoint> points, Func<ForecastPoint, double?> selector)
    {
        ForecastPoint? best = null;
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

    public static ForecastPoint? HighestBy(this IEnumerable<ForecastPoint> points, Func<ForecastPoint, double?> selector)
    {
        ForecastPoint? best = null;
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
