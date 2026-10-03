using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed class EnrichmentSnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("enrichments")] public Dictionary<string, EnrichmentEntryDto> Enrichments { get; set; } = new();
}

public sealed class EnrichmentEntryDto
{
    [JsonProperty("type")] public string TypeName { get; set; } = "";
    [JsonProperty("json")] public string JsonPayload { get; set; } = "";
}
