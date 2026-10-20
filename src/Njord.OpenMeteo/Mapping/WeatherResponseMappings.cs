using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Weather;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Mapping;

internal static class WeatherResponseMappings
{
    public static DomainForecast<WeatherPoint>? ToDomainForecast(
        this WeatherResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var times = h.Time;
        var builder = ImmutableArray.CreateBuilder<WeatherPoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            if (AnyAt(i, h.Temperature, h.ApparentTemperature, h.WindSpeed, h.Precipitation,
                    h.PrecipitationProbability, h.CloudCover, h.Pressure, h.WeatherCode, h.Humidity,
                    h.DewPoint, h.UvIndex, h.EnsembleStdDev, h.WindGusts, h.Visibility, h.Cape, h.Rain,
                    h.Snowfall, h.SoilTemperature, h.FreezingLevelHeight, h.IsDay, h.ShortwaveRadiation,
                    h.DirectRadiation, h.DiffuseRadiation, h.DirectNormalIrradiance, h.SunshineDuration,
                    h.WindDirection))
            {
                lastValued = i;
            }

            builder.Add(new WeatherPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(times[i]),
                Temperature: At(h.Temperature, i),
                ApparentTemperature: At(h.ApparentTemperature, i),
                WindSpeed: At(h.WindSpeed, i),
                Precipitation: At(h.Precipitation, i),
                PrecipitationProbability: At(h.PrecipitationProbability, i),
                CloudCover: At(h.CloudCover, i),
                Pressure: At(h.Pressure, i),
                WeatherCode: At(h.WeatherCode, i) is { } code ? (int)code : null,
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
                IsDay: At(h.IsDay, i) is { } isDay ? isDay > 0.5 : null,
                ShortwaveRadiation: At(h.ShortwaveRadiation, i),
                DirectRadiation: At(h.DirectRadiation, i),
                DiffuseRadiation: At(h.DiffuseRadiation, i),
                DirectNormalIrradiance: At(h.DirectNormalIrradiance, i),
                SunshineDuration: At(h.SunshineDuration, i),
                WindDirection: At(h.WindDirection, i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<WeatherPoint>(
            DomainKind.Weather, modelRun, TimeResolution.Hourly,
            builder.Take(lastValued + 1).ToImmutableArray());
    }
}
