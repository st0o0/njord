using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record ClimateDailyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m_mean")] double?[] TemperatureMean,
    [property: JsonPropertyName("precipitation_sum")] double?[]? PrecipitationSum = null,
    [property: JsonPropertyName("sunshine_duration")] double?[]? SunshineDuration = null);

public sealed record ClimateResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("daily")] ClimateDailyData? Daily);
