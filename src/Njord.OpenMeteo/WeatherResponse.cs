using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record WeatherHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m")] double?[] Temperature,
    [property: JsonPropertyName("apparent_temperature")] double?[] ApparentTemperature,
    [property: JsonPropertyName("wind_speed_10m")] double?[] WindSpeed,
    [property: JsonPropertyName("precipitation")] double?[] Precipitation,
    [property: JsonPropertyName("precipitation_probability")] double?[] PrecipitationProbability,
    [property: JsonPropertyName("cloud_cover")] double?[] CloudCover,
    [property: JsonPropertyName("pressure_msl")] double?[] Pressure,
    [property: JsonPropertyName("weather_code")] double?[] WeatherCode,
    [property: JsonPropertyName("relative_humidity_2m")] double?[] Humidity,
    [property: JsonPropertyName("dew_point_2m")] double?[] DewPoint,
    [property: JsonPropertyName("uv_index")] double?[] UvIndex,
    [property: JsonPropertyName("temperature_2m_std")] double?[]? EnsembleStdDev = null,
    [property: JsonPropertyName("wind_gusts_10m")] double?[]? WindGusts = null,
    [property: JsonPropertyName("visibility")] double?[]? Visibility = null,
    [property: JsonPropertyName("cape")] double?[]? Cape = null,
    [property: JsonPropertyName("rain")] double?[]? Rain = null,
    [property: JsonPropertyName("snowfall")] double?[]? Snowfall = null,
    [property: JsonPropertyName("soil_temperature_0cm")] double?[]? SoilTemperature = null,
    [property: JsonPropertyName("freezing_level_height")] double?[]? FreezingLevelHeight = null,
    [property: JsonPropertyName("is_day")] double?[]? IsDay = null,
    [property: JsonPropertyName("shortwave_radiation")] double?[]? ShortwaveRadiation = null,
    [property: JsonPropertyName("direct_radiation")] double?[]? DirectRadiation = null,
    [property: JsonPropertyName("diffuse_radiation")] double?[]? DiffuseRadiation = null,
    [property: JsonPropertyName("direct_normal_irradiance")] double?[]? DirectNormalIrradiance = null,
    [property: JsonPropertyName("sunshine_duration")] double?[]? SunshineDuration = null);

public sealed record WeatherResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] WeatherHourlyData? Hourly);
