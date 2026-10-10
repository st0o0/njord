using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record PersistedForecastHistory
{
    [JsonProperty("retention")] public int RetentionDays { get; init; }
    [JsonProperty("records")] public List<PersistedForecastRecord> Records { get; init; } = [];
}
