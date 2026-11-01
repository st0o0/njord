using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Seasonal;

public enum SeasonalApiVariable
{
    [JsonStringEnumMemberName("temperature_2m_anomaly")]
    TemperatureAnomaly,
    [JsonStringEnumMemberName("precipitation_anomaly")]
    PrecipitationAnomaly,
    [JsonStringEnumMemberName("precipitation_mean")]
    PrecipitationMean,
    [JsonStringEnumMemberName("temperature_2m")]
    Temperature,
    [JsonStringEnumMemberName("snowfall")]
    Snowfall,
    [JsonStringEnumMemberName("snow_depth")]
    SnowDepth,
    [JsonStringEnumMemberName("cloud_cover")]
    CloudCover,
    [JsonStringEnumMemberName("wind_speed_10m")]
    WindSpeed,
    [JsonStringEnumMemberName("pressure_msl")]
    Pressure,
    [JsonStringEnumMemberName("sunshine_duration")]
    SunshineDuration,
    [JsonStringEnumMemberName("shortwave_radiation")]
    ShortwaveRadiation,
    [JsonStringEnumMemberName("wind_gusts_10m")]
    WindGusts,
    [JsonStringEnumMemberName("sea_surface_temperature")]
    SeaSurfaceTemperature,
}
