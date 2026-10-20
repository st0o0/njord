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
