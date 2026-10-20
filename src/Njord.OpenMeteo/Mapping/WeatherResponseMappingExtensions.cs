using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Weather;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Mapping;

internal static class WeatherResponseMappingExtensions
{
    // One trimmed forecast per model. Several models arrive suffixed; a single model arrives plain and is keyed by
    // the requested id. A model whose values are all null is omitted.
    public static ImmutableDictionary<string, DomainForecast<WeatherPoint>> ToModelForecasts(
        this WeatherResponse response, DateTimeOffset modelRun, string singleModelId)
    {
        var hourly = response.Models.IsEmpty
            ? response.Hourly is { } plain
                ? ImmutableDictionary<string, WeatherHourlyData>.Empty.Add(singleModelId, plain)
                : ImmutableDictionary<string, WeatherHourlyData>.Empty
            : response.Models;

        var forecasts = ImmutableDictionary.CreateBuilder<string, DomainForecast<WeatherPoint>>();
        foreach (var (model, data) in hourly)
        {
            if (data.ToForecast(modelRun)?.TrimTrailing((WeatherPoint p, WeatherParams f) => p.HasFields(f)) is { } forecast)
            {
                forecasts[model] = forecast;
            }
        }

        return forecasts.ToImmutable();
    }

    public static DomainForecast<WeatherPoint>? ToDomainForecast(
        this WeatherResponse response, DateTimeOffset modelRun) =>
        response.Hourly?.ToForecast(modelRun);

    private static DomainForecast<WeatherPoint>? ToForecast(this WeatherHourlyData h, DateTimeOffset modelRun)
    {
        if (h.Time.Length == 0)
        {
            return null;
        }

        var points = new WeatherPoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new WeatherPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
                Temperature: At(h.Temperature, i),
                ApparentTemperature: At(h.ApparentTemperature, i),
                WindSpeed: At(h.WindSpeed, i),
                Precipitation: At(h.Precipitation, i),
                PrecipitationProbability: At(h.PrecipitationProbability, i),
                CloudCover: At(h.CloudCover, i),
                Pressure: At(h.Pressure, i),
                WeatherCode: At(h.WeatherCode, i),
                Humidity: At(h.Humidity, i),
                DewPoint: At(h.DewPoint, i),
                UvIndex: At(h.UvIndex, i),
                EnsembleStdDev: At(h.EnsembleStdDev, i),
                WindGusts: At(h.WindGusts, i),
                Visibility: At(h.Visibility, i),
                Cape: At(h.Cape, i),
                Rain: At(h.Rain, i),
                Snowfall: At(h.Snowfall, i),
                SoilTemperature: At(h.SoilTemperature, i),
                FreezingLevelHeight: At(h.FreezingLevelHeight, i),
                IsDay: At(h.IsDay, i),
                ShortwaveRadiation: At(h.ShortwaveRadiation, i),
                DirectRadiation: At(h.DirectRadiation, i),
                DiffuseRadiation: At(h.DiffuseRadiation, i),
                DirectNormalIrradiance: At(h.DirectNormalIrradiance, i),
                SunshineDuration: At(h.SunshineDuration, i),
                WindDirection: At(h.WindDirection, i),
                GlobalTiltedIrradiance: At(h.GlobalTiltedIrradiance, i),
                SnowDepth: At(h.SnowDepth, i),
                WetBulbTemperature: At(h.WetBulbTemperature, i),
                SnowfallHeight: At(h.SnowfallHeight, i),
                PrecipitationSpread: At(h.PrecipitationSpread, i),
                WindSpeedSpread: At(h.WindSpeedSpread, i));
        }

        return new DomainForecast<WeatherPoint>(
            DomainKind.Weather, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
