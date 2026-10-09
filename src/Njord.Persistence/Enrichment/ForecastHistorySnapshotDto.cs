using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ForecastHistorySnapshotDto
{
    [JsonProperty("retention")] public int RetentionDays { get; init; }
    [JsonProperty("records")] public List<ForecastRecordDto> Records { get; init; } = [];
}
