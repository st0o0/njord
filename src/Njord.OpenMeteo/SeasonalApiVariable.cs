using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public enum SeasonalApiVariable
{
    [JsonStringEnumMemberName("temperature_2m_anomaly")]
    TemperatureAnomaly,
    [JsonStringEnumMemberName("precipitation_anomaly")]
    PrecipitationAnomaly,
    [JsonStringEnumMemberName("precipitation_mean")]
    PrecipitationMean,
}
