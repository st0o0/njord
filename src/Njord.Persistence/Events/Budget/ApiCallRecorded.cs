using Newtonsoft.Json;

namespace Njord.Persistence.Events.Budget;

public sealed record ApiCallRecorded
{
    [JsonProperty("w")] public int Weight { get; init; }
    [JsonProperty("utc")] public long UtcTicks { get; init; }
}
