using System.Collections.Immutable;
using Njord.Model;

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

        for (var i = 0; i < times.Length; i++)
        {
            builder.Add(new WeatherPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(times[i]),
                Temperature: At(h.Temperature, i) ?? 0,
                ApparentTemperature: At(h.ApparentTemperature, i) ?? 0,
                WindSpeed: At(h.WindSpeed, i) ?? 0,
                Precipitation: At(h.Precipitation, i) ?? 0,
                PrecipitationProbability: At(h.PrecipitationProbability, i) ?? 0,
                CloudCover: At(h.CloudCover, i) ?? 0,
                Pressure: At(h.Pressure, i) ?? 0,
                WeatherCode: (int)(At(h.WeatherCode, i) ?? 0),
                Humidity: At(h.Humidity, i) ?? 0,
                DewPoint: At(h.DewPoint, i) ?? 0,
                UvIndex: At(h.UvIndex, i) ?? 0,
                EnsembleStdDev: OptAt(h.EnsembleStdDev, i),
                WindGusts: OptAt(h.WindGusts, i),
                Visibility: OptAt(h.Visibility, i),
                Cape: OptAt(h.Cape, i),
                Rain: OptAt(h.Rain, i),
                Snowfall: OptAt(h.Snowfall, i),
                SoilTemperature: OptAt(h.SoilTemperature, i),
                FreezingLevelHeight: OptAt(h.FreezingLevelHeight, i),
                IsDay: OptAt(h.IsDay, i) is { } isDay ? isDay > 0.5 : null,
                ShortwaveRadiation: OptAt(h.ShortwaveRadiation, i),
                DirectRadiation: OptAt(h.DirectRadiation, i),
                DiffuseRadiation: OptAt(h.DiffuseRadiation, i),
                DirectNormalIrradiance: OptAt(h.DirectNormalIrradiance, i),
                SunshineDuration: OptAt(h.SunshineDuration, i),
                WindDirection: OptAt(h.WindDirection, i)));
        }

        var lastValued = FindLastValuedIndex(builder);
        if (lastValued < 0)
        {
            return null;
        }

        var points = lastValued < builder.Count - 1
            ? builder.Take(lastValued + 1).ToImmutableArray()
            : builder.ToImmutable();

        return new DomainForecast<WeatherPoint>(DomainKind.Weather, modelRun, TimeResolution.Hourly, points);
    }

    private static double? At(double?[] array, int index) =>
        index < array.Length ? array[index] : null;

    private static double? OptAt(double?[]? array, int index) =>
        array is not null && index < array.Length ? array[index] : null;

    private static int FindLastValuedIndex(ImmutableArray<WeatherPoint>.Builder points)
    {
        for (var i = points.Count - 1; i >= 0; i--)
        {
            var p = points[i];
            if (p.Temperature != 0 || p.WindSpeed != 0 || p.Precipitation != 0)
            {
                return i;
            }
        }

        return -1;
    }
}
