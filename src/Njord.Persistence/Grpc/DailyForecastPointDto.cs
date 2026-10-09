using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record DailyForecastPointDto
{
    [JsonProperty("date")] public string Date { get; init; } = "";
    [JsonProperty("num")] public Dictionary<string, double?> NumericValues { get; init; } = new();
    [JsonProperty("meta")] public Dictionary<string, string?> MetaValues { get; init; } = new();
}
