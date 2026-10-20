using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

// Monthly ensemble-mean values of the ECMWF SEAS5 seasonal forecast. Temperature anomalies are in K (equal to
// degrees Celsius), precipitation anomaly and mean in mm; the normal monthly precipitation is mean minus anomaly.
public sealed record SeasonalMonthlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m_anomaly")] double?[]? TemperatureAnomaly = null,
    [property: JsonPropertyName("precipitation_anomaly")] double?[]? PrecipitationAnomaly = null,
    [property: JsonPropertyName("precipitation_mean")] double?[]? PrecipitationMean = null);

public sealed record SeasonalResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("monthly")] SeasonalMonthlyData? Monthly);
