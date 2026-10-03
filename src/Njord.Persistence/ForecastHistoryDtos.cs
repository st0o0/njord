using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed class ForecastRecordDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("ts")] public long TimestampUtcTicks { get; set; }
    [JsonProperty("loc")] public string Location { get; set; } = "";
    [JsonProperty("models")] public Dictionary<string, Dictionary<string, double?>> ModelValues { get; set; } = new();
    [JsonProperty("consensus")] public Dictionary<string, double?> ConsensusValues { get; set; } = new();
}

public sealed class ForecastHistorySnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("retention")] public int RetentionDays { get; set; }
    [JsonProperty("records")] public List<ForecastRecordDto> Records { get; set; } = [];
}
