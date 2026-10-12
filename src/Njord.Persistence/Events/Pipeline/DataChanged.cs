using Newtonsoft.Json;

namespace Njord.Persistence.Events.Pipeline;

public sealed record DataChanged
{
    [JsonProperty("loc")] public string Location { get; init; } = "";
    [JsonProperty("model")] public string ModelId { get; init; } = "";
    [JsonProperty("hash")] public int Hash { get; init; }
    [JsonProperty("utc")] public long UtcTicks { get; init; }
}
