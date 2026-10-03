using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed class DataChangedDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("loc")] public string Location { get; set; } = "";
    [JsonProperty("model")] public string ModelId { get; set; } = "";
    [JsonProperty("hash")] public int Hash { get; set; }
    [JsonProperty("utc")] public long UtcTicks { get; set; }
}

public sealed class ModelPollStateDto
{
    [JsonProperty("hash")] public int? LastHash { get; set; }
    [JsonProperty("lastChange")] public long? LastChangeUtcTicks { get; set; }
    [JsonProperty("prevChange")] public long? PrevChangeUtcTicks { get; set; }
    [JsonProperty("next")] public long NextPollUtcTicks { get; set; }
    [JsonProperty("miss")] public int MissCount { get; set; }
    [JsonProperty("phase")] public string Phase { get; set; } = "Discovery";
    [JsonProperty("cycle")] public long? CycleTicks { get; set; }
}

public sealed class SchedulerSnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("states")] public Dictionary<string, ModelPollStateDto> States { get; set; } = new();
}
