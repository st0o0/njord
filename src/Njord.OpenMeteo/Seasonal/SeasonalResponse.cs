using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Seasonal;

// Monthly ensemble-mean values of the ECMWF seasonal forecast. Temperature anomalies are in K (equal to
// degrees Celsius), precipitation anomaly and mean in mm; the normal monthly precipitation is mean minus anomaly.
public sealed record SeasonalMonthlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m_anomaly")] double?[]? TemperatureAnomaly = null,
    [property: JsonPropertyName("precipitation_anomaly")] double?[]? PrecipitationAnomaly = null,
    [property: JsonPropertyName("precipitation_mean")] double?[]? PrecipitationMean = null);

// Weekly probabilistic values: Extreme Forecast Index (-1..1), Shift of Tails and the probability (0-100 %) that the
// weekly anomaly exceeds a threshold.
public sealed record SeasonalWeeklyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m_efi")] double?[]? TemperatureEfi = null,
    [property: JsonPropertyName("precipitation_efi")] double?[]? PrecipitationEfi = null,
    [property: JsonPropertyName("temperature_2m_sot10")] double?[]? TemperatureSot10 = null,
    [property: JsonPropertyName("temperature_2m_sot90")] double?[]? TemperatureSot90 = null,
    [property: JsonPropertyName("precipitation_sot90")] double?[]? PrecipitationSot90 = null,
    [property: JsonPropertyName("temperature_2m_anomaly_gt0")] double?[]? TemperatureAnomalyGt0 = null,
    [property: JsonPropertyName("temperature_2m_anomaly_gt1")] double?[]? TemperatureAnomalyGt1 = null,
    [property: JsonPropertyName("temperature_2m_anomaly_gt2")] double?[]? TemperatureAnomalyGt2 = null,
    [property: JsonPropertyName("temperature_2m_anomaly_ltm1")] double?[]? TemperatureAnomalyLtM1 = null,
    [property: JsonPropertyName("temperature_2m_anomaly_ltm2")] double?[]? TemperatureAnomalyLtM2 = null,
    [property: JsonPropertyName("precipitation_anomaly_gt0")] double?[]? PrecipitationAnomalyGt0 = null,
    [property: JsonPropertyName("precipitation_anomaly_gt10")] double?[]? PrecipitationAnomalyGt10 = null,
    [property: JsonPropertyName("precipitation_anomaly_gt20")] double?[]? PrecipitationAnomalyGt20 = null);

public sealed record SeasonalResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("monthly")] SeasonalMonthlyData? Monthly,
    [property: JsonPropertyName("weekly")] SeasonalWeeklyData? Weekly = null);
