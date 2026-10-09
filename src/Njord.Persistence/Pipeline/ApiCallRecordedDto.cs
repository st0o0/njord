using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ApiCallRecordedDto
{
    [JsonProperty("w")] public int Weight { get; init; }
    [JsonProperty("utc")] public long UtcTicks { get; init; }
}
