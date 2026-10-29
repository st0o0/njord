using Njord.Core;
using Njord.Core.Forecast;

namespace Njord.OpenMeteo.Mapping;

internal static class ForecastTrimmingExtensions
{
    public static DomainForecast<T>? TrimTrailing<T, TParams>(
        this DomainForecast<T> forecast, Func<T, TParams, bool> hasFields)
        where T : IHasValidTime
        where TParams : struct, Enum
    {
        var flags = Enum.GetValues<TParams>().Where(f => Convert.ToInt64(f) != 0).ToArray();
        var last = -1;
        for (var i = 0; i < forecast.Points.Length; i++)
        {
            if (flags.Any(f => hasFields(forecast.Points[i], f)))
            {
                last = i;
            }
        }

        return last < 0
            ? null
            : forecast with { Points = [.. forecast.Points.Take(last + 1)] };
    }
}
