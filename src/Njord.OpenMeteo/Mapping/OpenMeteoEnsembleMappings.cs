using System.Collections.Immutable;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoEnsembleMappings
{
    // Maps an Open-Meteo ensemble response to a DomainForecast<WeatherPoint>.
    // The ensemble endpoint returns hourly data with the regular parameter names for mean values.
    // Standard deviation may be available as "{param}_std" if the API returns it.
    public static DomainForecast<WeatherPoint>? ToWeatherForecast(
        this OpenMeteoResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } series || series.Time.Length == 0)
        {
            return null;
        }

        var times = series.Time;
        var builder = ImmutableArray.CreateBuilder<WeatherPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            var validFrom = DateTimeOffset.FromUnixTimeSeconds(times[i]);

            var point = new WeatherPoint(
                validFrom,
                Temperature: GetDouble(series, "temperature_2m", i),
                ApparentTemperature: GetDouble(series, "apparent_temperature", i),
                WindSpeed: GetDouble(series, "wind_speed_10m", i),
                Precipitation: GetDouble(series, "precipitation", i),
                PrecipitationProbability: GetDouble(series, "precipitation_probability", i),
                CloudCover: GetDouble(series, "cloud_cover", i),
                Pressure: GetDouble(series, "pressure_msl", i),
                WeatherCode: (int)GetDouble(series, "weather_code", i),
                Humidity: GetDouble(series, "relative_humidity_2m", i),
                DewPoint: GetDouble(series, "dew_point_2m", i),
                UvIndex: GetDouble(series, "uv_index", i),
                EnsembleStdDev: GetOptionalDouble(series, "temperature_2m_std", i),
                WindGusts: GetOptionalDouble(series, "wind_gusts_10m", i),
                Visibility: GetOptionalDouble(series, "visibility", i),
                Cape: GetOptionalDouble(series, "cape", i),
                Rain: GetOptionalDouble(series, "rain", i),
                Snowfall: GetOptionalDouble(series, "snowfall", i),
                SoilTemperature: GetOptionalDouble(series, "soil_temperature_0cm", i),
                FreezingLevelHeight: GetOptionalDouble(series, "freezing_level_height", i),
                IsDay: GetOptionalDouble(series, "is_day", i) is { } isDay ? isDay > 0.5 : null,
                ShortwaveRadiation: GetOptionalDouble(series, "shortwave_radiation", i),
                DirectRadiation: GetOptionalDouble(series, "direct_radiation", i),
                DiffuseRadiation: GetOptionalDouble(series, "diffuse_radiation", i),
                DirectNormalIrradiance: GetOptionalDouble(series, "direct_normal_irradiance", i),
                SunshineDuration: GetOptionalDouble(series, "sunshine_duration", i));

            builder.Add(point);
        }

        // Trim trailing all-default points (where all values are zero or default)
        var lastValued = FindLastValuedIndex(builder);
        if (lastValued < 0)
        {
            return null;
        }

        var points = lastValued < builder.Count - 1
            ? builder.Take(lastValued + 1).ToImmutableArray()
            : builder.ToImmutable();

        return new DomainForecast<WeatherPoint>(
            DomainKind.Weather,
            modelRun,
            TimeResolution.Hourly,
            points);
    }

    private static double GetDouble(OpenMeteoSeries series, string name, int index) =>
        series.ValueAt(name, index) ?? 0;

    private static double? GetOptionalDouble(OpenMeteoSeries series, string name, int index) =>
        series.ValueAt(name, index);

    private static int FindLastValuedIndex(ImmutableArray<WeatherPoint>.Builder points)
    {
        for (var i = points.Count - 1; i >= 0; i--)
        {
            var p = points[i];
            // A point is "valued" if it has non-zero temperature (a reasonable proxy for
            // "the model returned data for this timestep")
            if (p.Temperature != 0 || p.WindSpeed != 0 || p.Precipitation != 0)
            {
                return i;
            }
        }

        return -1;
    }
}
