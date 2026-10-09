using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record EnrichmentSnapshotDto
{
    [JsonProperty("enrichments")] public Dictionary<string, EnrichmentEntryDto> Enrichments { get; init; } = new();
}
