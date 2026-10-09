using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record EnrichmentEntryDto
{
    [JsonProperty("type")] public string TypeName { get; init; } = "";
    [JsonProperty("json")] public string JsonPayload { get; init; } = "";
}
