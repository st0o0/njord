using System.Text.Json.Serialization;

namespace Njord.Ingest;

// Response shape verified against the live API (2026-07-11): single-model
// requests return flat arrays with unsuffixed variable names. Unmapped
// response fields are intentionally ignored during deserialization.
internal sealed record OpenMeteoForecastResponse(
    [property: JsonPropertyName("hourly_units")] OpenMeteoHourlyUnits? HourlyUnits,
    [property: JsonPropertyName("hourly")] OpenMeteoHourly? Hourly);

internal sealed record OpenMeteoHourlyUnits(
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("temperature_2m")] string? Temperature2m,
    [property: JsonPropertyName("apparent_temperature")] string? ApparentTemperature,
    [property: JsonPropertyName("wind_speed_10m")] string? WindSpeed10m,
    [property: JsonPropertyName("wind_gusts_10m")] string? WindGusts10m);

internal sealed record OpenMeteoHourly(
    [property: JsonPropertyName("time")] IReadOnlyList<long> Time,
    [property: JsonPropertyName("temperature_2m")] IReadOnlyList<double?>? Temperature2m,
    [property: JsonPropertyName("apparent_temperature")] IReadOnlyList<double?>? ApparentTemperature,
    [property: JsonPropertyName("precipitation")] IReadOnlyList<double?>? Precipitation,
    [property: JsonPropertyName("wind_speed_10m")] IReadOnlyList<double?>? WindSpeed10m,
    [property: JsonPropertyName("wind_gusts_10m")] IReadOnlyList<double?>? WindGusts10m,
    [property: JsonPropertyName("dew_point_2m")] IReadOnlyList<double?>? DewPoint2m,
    [property: JsonPropertyName("relative_humidity_2m")] IReadOnlyList<double?>? RelativeHumidity2m,
    [property: JsonPropertyName("cloud_cover")] IReadOnlyList<double?>? CloudCover,
    [property: JsonPropertyName("pressure_msl")] IReadOnlyList<double?>? PressureMsl);

// Error payload of non-2xx responses: {"error":true,"reason":"..."} (verified).
internal sealed record OpenMeteoErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("reason")] string? Reason);
