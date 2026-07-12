using Njord.Domain;

namespace Njord.Egress;

/// <summary>Egress-facing names and HA metadata for the closed parameter set.</summary>
public static class ParameterKeys
{
    /// <summary>Snake-case key used in state JSON, unique_ids, and value templates.</summary>
    public static string JsonKey(this WeatherParameter parameter) => parameter switch
    {
        WeatherParameter.Temperature => "temperature",
        WeatherParameter.ApparentTemperature => "apparent_temperature",
        WeatherParameter.Precipitation => "precipitation",
        WeatherParameter.WindSpeed => "wind_speed",
        WeatherParameter.WindGust => "wind_gust",
        WeatherParameter.Dewpoint => "dewpoint",
        WeatherParameter.RelativeHumidity => "relative_humidity",
        WeatherParameter.CloudCover => "cloud_cover",
        WeatherParameter.PressureMsl => "pressure_msl",
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };

    /// <summary>HA device_class, or null where none fits (cloud cover has no device class).</summary>
    public static string? DeviceClass(this WeatherParameter parameter) => parameter switch
    {
        WeatherParameter.Temperature or WeatherParameter.ApparentTemperature or WeatherParameter.Dewpoint
            => "temperature",
        WeatherParameter.Precipitation => "precipitation",
        WeatherParameter.WindSpeed or WeatherParameter.WindGust => "wind_speed",
        WeatherParameter.RelativeHumidity => "humidity",
        WeatherParameter.CloudCover => null,
        WeatherParameter.PressureMsl => "atmospheric_pressure",
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };
}
