using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Seasonal;
using Njord.OpenMeteo.Mapping;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Seasonal;

internal static class SeasonalResponseMappingExtensions
{
    // The API always returns about six months starting with the current one; the first months after the current
    // month are kept (forecastMonths of them plus the current month itself).
    public static DomainForecast<SeasonalPoint>? ToDomainForecast(
        this SeasonalResponse response, DateTimeOffset modelRun, int forecastMonths)
    {
        if (response.Monthly is not { Time.Length: > 0 } m)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<SeasonalPoint>();
        for (var i = 0; i < m.Time.Length && i <= forecastMonths; i++)
        {
            builder.Add(new SeasonalPoint(
                DateTimeOffset.FromUnixTimeSeconds(m.Time[i]),
                TemperatureAnomaly: At(m.TemperatureAnomaly, i),
                PrecipitationAnomaly: At(m.PrecipitationAnomaly, i),
                PrecipitationMean: At(m.PrecipitationMean, i)));
        }

        return builder.Any(p => p.TemperatureAnomaly is not null || p.PrecipitationAnomaly is not null)
            ? new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, modelRun, TimeResolution.Monthly, builder.ToImmutable())
            : null;
    }
}
