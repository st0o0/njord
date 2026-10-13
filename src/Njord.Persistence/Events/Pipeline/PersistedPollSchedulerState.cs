using Newtonsoft.Json;

namespace Njord.Persistence.Events.Pipeline;

public sealed record PersistedPollSchedulerState
{
    [JsonProperty("entries")] public PersistedModelPollEntry[] Entries { get; init; } = [];
}

public sealed record PersistedModelPollEntry
{
    [JsonProperty("key")] public string Key { get; init; } = "";
    [JsonProperty("hash")] public int? LastHash { get; init; }
    [JsonProperty("changed")] public long? LastChangeUtcTicks { get; init; }
    [JsonProperty("prev")] public long? PrevChangeUtcTicks { get; init; }
    [JsonProperty("miss")] public int MissCount { get; init; }
    [JsonProperty("phase")] public int Phase { get; init; }
    [JsonProperty("cycle")] public long? CycleTicks { get; init; }
    [JsonProperty("tfc")] public int TransientFailureCount { get; init; }
}
