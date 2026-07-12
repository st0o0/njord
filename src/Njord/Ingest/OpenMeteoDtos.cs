using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.Ingest;

internal sealed record OpenMeteoForecastResponse(
    [property: JsonPropertyName("hourly_units")] JsonElement? HourlyUnits,
    [property: JsonPropertyName("hourly")] JsonElement? Hourly,
    [property: JsonPropertyName("daily_units")] JsonElement? DailyUnits,
    [property: JsonPropertyName("daily")] JsonElement? Daily);

internal sealed record OpenMeteoErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("reason")] string? Reason);
