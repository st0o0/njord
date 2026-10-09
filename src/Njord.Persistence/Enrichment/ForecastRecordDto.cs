using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ForecastRecordDto
{
    [JsonProperty("ts")] public long TimestampUtcTicks { get; init; }
    [JsonProperty("loc")] public string Location { get; init; } = "";
    [JsonProperty("models")] public Dictionary<string, Dictionary<string, double?>> ModelValues { get; init; } = new();
    [JsonProperty("consensus")] public Dictionary<string, double?> ConsensusValues { get; init; } = new();
}
