using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record DataChangedDto
{
    [JsonProperty("loc")] public string Location { get; init; } = "";
    [JsonProperty("model")] public string ModelId { get; init; } = "";
    [JsonProperty("hash")] public int Hash { get; init; }
    [JsonProperty("utc")] public long UtcTicks { get; init; }
}
