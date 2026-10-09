using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ModelPollStateDto
{
    [JsonProperty("hash")] public int? LastHash { get; init; }
    [JsonProperty("lastChange")] public long? LastChangeUtcTicks { get; init; }
    [JsonProperty("prevChange")] public long? PrevChangeUtcTicks { get; init; }
    [JsonProperty("next")] public long NextPollUtcTicks { get; init; }
    [JsonProperty("miss")] public int MissCount { get; init; }
    [JsonProperty("phase")] public string Phase { get; init; } = "Discovery";
    [JsonProperty("cycle")] public long? CycleTicks { get; init; }
}
