using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.Ingest;

internal sealed record OpenMeteoForecastResponse(
    [property: JsonPropertyName("hourly_units")] Dictionary<string, string>? HourlyUnits,
    [property: JsonPropertyName("hourly")] OpenMeteoTimeSeries? Hourly,
    [property: JsonPropertyName("daily_units")] Dictionary<string, string>? DailyUnits,
    [property: JsonPropertyName("daily")] OpenMeteoTimeSeries? Daily);

internal sealed class OpenMeteoTimeSeries
{
    [JsonPropertyName("time")]
    public IReadOnlyList<long> Time { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Variables { get; set; } = [];
}

internal sealed record OpenMeteoErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("reason")] string? Reason);
