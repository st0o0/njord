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
        if (response.Hourly is not var (times, temperature, apparentTemperature, windSpeed, precipitation, precipitationProbability, cloudCover, pressure, weatherCode, humidity, dewPoint, uvIndex, ensembleStdDev, windGusts, visibility, cape, rain, snowfall, soilTemperature, freezingLevelHeight, d, shortwaveRadiation, directRadiation, diffuseRadiation, directNormalIrradiance, sunshineDuration, windDirection, globalTiltedIrradiance, snowDepth, wetBulbTemperature, snowfallHeight, precipitationSpread, windSpeedSpread) || times.Length == 0)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<WeatherPoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            if (AnyAt(i, temperature, apparentTemperature, windSpeed, precipitation,
                    precipitationProbability, cloudCover, pressure, weatherCode, humidity,
                    dewPoint, uvIndex, ensembleStdDev, windGusts, visibility, cape, rain,
                    snowfall, soilTemperature, freezingLevelHeight, d, shortwaveRadiation,
                    directRadiation, diffuseRadiation, directNormalIrradiance, sunshineDuration,
                    windDirection, globalTiltedIrradiance, snowDepth, wetBulbTemperature, snowfallHeight, precipitationSpread, windSpeedSpread))
            {
                lastValued = i;
            }

            builder.Add(new WeatherPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(times[i]),
                Temperature: At(temperature, i),
                ApparentTemperature: At(apparentTemperature, i),
                WindSpeed: At(windSpeed, i),
                Precipitation: At(precipitation, i),
                PrecipitationProbability: At(precipitationProbability, i),
                CloudCover: At(cloudCover, i),
                Pressure: At(pressure, i),
                WeatherCode: At(weatherCode, i) is { } code ? (int)code : null,
                Humidity: At(humidity, i),
                DewPoint: At(dewPoint, i),
                UvIndex: At(uvIndex, i),
                EnsembleStdDev: At(ensembleStdDev, i),
                WindGusts: At(windGusts, i),
                Visibility: At(visibility, i),
                Cape: At(cape, i),
                Rain: At(rain, i),
                Snowfall: At(snowfall, i),
                SoilTemperature: At(soilTemperature, i),
                FreezingLevelHeight: At(freezingLevelHeight, i),
                IsDay: At(d, i) is { } isDay ? isDay > 0.5 : null,
                ShortwaveRadiation: At(shortwaveRadiation, i),
                DirectRadiation: At(directRadiation, i),
                DiffuseRadiation: At(diffuseRadiation, i),
                DirectNormalIrradiance: At(directNormalIrradiance, i),
                SunshineDuration: At(sunshineDuration, i),
                WindDirection: At(windDirection, i),
                GlobalTiltedIrradiance: At(globalTiltedIrradiance, i),
                SnowDepth: At(snowDepth, i),
                WetBulbTemperature: At(wetBulbTemperature, i),
                SnowfallHeight: At(snowfallHeight, i),
                PrecipitationSpread: At(precipitationSpread, i),
                WindSpeedSpread: At(windSpeedSpread, i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<WeatherPoint>(
            DomainKind.Weather, modelRun, TimeResolution.Hourly,
            [.. builder.Take(lastValued + 1)]);
    }
}
