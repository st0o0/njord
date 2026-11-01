using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Seasonal;

public enum SeasonalWeeklyApiVariable
{
    [JsonStringEnumMemberName("temperature_2m_efi")]
    TemperatureEfi,
    [JsonStringEnumMemberName("precipitation_efi")]
    PrecipitationEfi,
    [JsonStringEnumMemberName("temperature_2m_sot10")]
    TemperatureSot10,
    [JsonStringEnumMemberName("temperature_2m_sot90")]
    TemperatureSot90,
    [JsonStringEnumMemberName("precipitation_sot90")]
    PrecipitationSot90,
    [JsonStringEnumMemberName("temperature_2m_anomaly_gt0")]
    TemperatureAnomalyGt0,
    [JsonStringEnumMemberName("temperature_2m_anomaly_gt1")]
    TemperatureAnomalyGt1,
    [JsonStringEnumMemberName("temperature_2m_anomaly_gt2")]
    TemperatureAnomalyGt2,
    [JsonStringEnumMemberName("temperature_2m_anomaly_ltm1")]
    TemperatureAnomalyLtM1,
    [JsonStringEnumMemberName("temperature_2m_anomaly_ltm2")]
    TemperatureAnomalyLtM2,
    [JsonStringEnumMemberName("precipitation_anomaly_gt0")]
    PrecipitationAnomalyGt0,
    [JsonStringEnumMemberName("precipitation_anomaly_gt10")]
    PrecipitationAnomalyGt10,
    [JsonStringEnumMemberName("precipitation_anomaly_gt20")]
    PrecipitationAnomalyGt20,
}
