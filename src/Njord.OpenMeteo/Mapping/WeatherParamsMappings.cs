using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class WeatherParamsMappings
{
    public static IReadOnlyList<string> ToApiNames(this WeatherParams flags) =>
        [.. Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(WeatherParams param) => param switch
    {
        WeatherParams.Temperature => "temperature_2m",
        WeatherParams.ApparentTemperature => "apparent_temperature",
        WeatherParams.WindSpeed => "wind_speed_10m",
        WeatherParams.Precipitation => "precipitation",
        WeatherParams.PrecipitationProbability => "precipitation_probability",
        WeatherParams.CloudCover => "cloud_cover",
        WeatherParams.Pressure => "pressure_msl",
        WeatherParams.WeatherCode => "weather_code",
        WeatherParams.Humidity => "relative_humidity_2m",
        WeatherParams.DewPoint => "dew_point_2m",
        WeatherParams.UvIndex => "uv_index",
        WeatherParams.EnsembleStdDev => "temperature_2m_spread",
        WeatherParams.WindGusts => "wind_gusts_10m",
        WeatherParams.Visibility => "visibility",
        WeatherParams.Cape => "cape",
        WeatherParams.Rain => "rain",
        WeatherParams.Snowfall => "snowfall",
        WeatherParams.SoilTemperature => "soil_temperature_0_to_10cm",
        WeatherParams.FreezingLevelHeight => "freezing_level_height",
        WeatherParams.IsDay => "is_day",
        WeatherParams.ShortwaveRadiation => "shortwave_radiation",
        WeatherParams.DirectRadiation => "direct_radiation",
        WeatherParams.DiffuseRadiation => "diffuse_radiation",
        WeatherParams.DirectNormalIrradiance => "direct_normal_irradiance",
        WeatherParams.SunshineDuration => "sunshine_duration",
        WeatherParams.WindDirection => "wind_direction_10m",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
