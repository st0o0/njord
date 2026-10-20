using System.Collections.Immutable;
using Njord.Forecast;

namespace Njord.OpenMeteo.Mapping;

internal static class ForecastBuilding
{
    public static DomainForecast<TPoint>? BuildForecast<TPoint>(
        DomainKind domain,
        TimeResolution resolution,
        DateTimeOffset modelRun,
        int count,
        Func<int, bool> hasValue,
        Func<int, TPoint> create)
        where TPoint : IHasValidTime
    {
        var builder = ImmutableArray.CreateBuilder<TPoint>(count);
        var lastValued = -1;

        for (var i = 0; i < count; i++)
        {
            if (hasValue(i))
            {
                lastValued = i;
            }

            builder.Add(create(i));
        }

        return lastValued < 0
            ? null
            : new DomainForecast<TPoint>(domain, modelRun, resolution, [.. builder.Take(lastValued + 1)]);
    }
}
