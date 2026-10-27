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
}
