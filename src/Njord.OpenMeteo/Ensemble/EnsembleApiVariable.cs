using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Ensemble;

public enum EnsembleApiVariable
{
    [JsonStringEnumMemberName("temperature_2m_spread")]
    TemperatureSpread,
    [JsonStringEnumMemberName("precipitation_spread")]
    PrecipitationSpread,
    [JsonStringEnumMemberName("wind_speed_10m_spread")]
    WindSpeedSpread,
    [JsonStringEnumMemberName("pressure_msl_spread")]
    PressureMslSpread,
    [JsonStringEnumMemberName("cloud_cover_spread")]
    CloudCoverSpread,
    [JsonStringEnumMemberName("wind_gusts_10m_spread")]
    WindGustsSpread,
    [JsonStringEnumMemberName("cape_spread")]
    CapeSpread,
    [JsonStringEnumMemberName("snowfall_spread")]
    SnowfallSpread,
    [JsonStringEnumMemberName("rain_spread")]
    RainSpread,
    [JsonStringEnumMemberName("dew_point_2m_spread")]
    DewPointSpread,
    [JsonStringEnumMemberName("relative_humidity_2m_spread")]
    HumiditySpread,
    [JsonStringEnumMemberName("surface_pressure_spread")]
    SurfacePressureSpread,
    [JsonStringEnumMemberName("wind_direction_10m_spread")]
    WindDirectionSpread,
    [JsonStringEnumMemberName("wet_bulb_temperature_2m_spread")]
    WetBulbTemperatureSpread,
    [JsonStringEnumMemberName("wind_speed_100m_spread")]
    WindSpeed100mSpread,
}
