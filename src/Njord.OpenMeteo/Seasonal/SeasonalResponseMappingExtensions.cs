using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.Forecast;
using Njord.Core.Seasonal;
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
                PrecipitationMean: At(m.PrecipitationMean, i),
                Temperature: At(m.Temperature, i),
                Snowfall: At(m.Snowfall, i),
                SnowDepth: At(m.SnowDepth, i),
                CloudCover: At(m.CloudCover, i),
                WindSpeed: At(m.WindSpeed, i),
                Pressure: At(m.Pressure, i),
                SunshineDuration: At(m.SunshineDuration, i),
                ShortwaveRadiation: At(m.ShortwaveRadiation, i),
                WindGusts: At(m.WindGusts, i),
                SeaSurfaceTemperature: At(m.SeaSurfaceTemperature, i)));
        }

        return builder.Any(p => p.TemperatureAnomaly is not null || p.PrecipitationAnomaly is not null || p.Temperature is not null)
            ? new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, modelRun, TimeResolution.Monthly, builder.ToImmutable())
            : null;
    }

    public static DomainForecast<SeasonalWeeklyPoint>? ToWeeklyDomainForecast(this SeasonalResponse response, DateTimeOffset modelRun)
    {
        if (response.Weekly is not { Time.Length: > 0 } w)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<SeasonalWeeklyPoint>();
        for (var i = 0; i < w.Time.Length; i++)
        {
            builder.Add(new SeasonalWeeklyPoint(
                DateTimeOffset.FromUnixTimeSeconds(w.Time[i]),
                TemperatureEfi: At(w.TemperatureEfi, i),
                PrecipitationEfi: At(w.PrecipitationEfi, i),
                TemperatureSot10: At(w.TemperatureSot10, i),
                TemperatureSot90: At(w.TemperatureSot90, i),
                PrecipitationSot90: At(w.PrecipitationSot90, i),
                TemperatureAnomalyGt0: At(w.TemperatureAnomalyGt0, i),
                TemperatureAnomalyGt1: At(w.TemperatureAnomalyGt1, i),
                TemperatureAnomalyGt2: At(w.TemperatureAnomalyGt2, i),
                TemperatureAnomalyLtM1: At(w.TemperatureAnomalyLtM1, i),
                TemperatureAnomalyLtM2: At(w.TemperatureAnomalyLtM2, i),
                PrecipitationAnomalyGt0: At(w.PrecipitationAnomalyGt0, i),
                PrecipitationAnomalyGt10: At(w.PrecipitationAnomalyGt10, i),
                PrecipitationAnomalyGt20: At(w.PrecipitationAnomalyGt20, i)));
        }

        return builder.Any(p => p.TemperatureEfi is not null || p.PrecipitationEfi is not null || p.TemperatureSot10 is not null
                || p.TemperatureSot90 is not null || p.PrecipitationSot90 is not null || p.TemperatureAnomalyGt0 is not null
                || p.TemperatureAnomalyGt1 is not null || p.TemperatureAnomalyGt2 is not null || p.TemperatureAnomalyLtM1 is not null
                || p.TemperatureAnomalyLtM2 is not null || p.PrecipitationAnomalyGt0 is not null
                || p.PrecipitationAnomalyGt10 is not null || p.PrecipitationAnomalyGt20 is not null)
            ? new DomainForecast<SeasonalWeeklyPoint>(DomainKind.Seasonal, modelRun, TimeResolution.Weekly, builder.ToImmutable())
            : null;
    }
}
