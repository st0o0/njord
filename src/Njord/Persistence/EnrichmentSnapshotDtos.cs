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

public static class EnrichmentSnapshotMapping
{
    private static readonly Dictionary<string, Type> EnrichmentTypes = new()
    {
        ["AlertResult"] = typeof(Domain.Analysis.AlertResult),
        ["IndexResult"] = typeof(Domain.Analysis.IndexResult),
        ["TrendResult"] = typeof(Domain.Analysis.TrendResult),
        ["DerivedResult"] = typeof(Domain.Analysis.DerivedResult),
        ["EnergyResult"] = typeof(Domain.Analysis.EnergyResult),
        ["ConsensusResult"] = typeof(Domain.Analysis.ConsensusResult),
    };

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Include,
    };

    public static EnrichmentSnapshotDto ToDto(Dictionary<string, object> state)
    {
        var dto = new EnrichmentSnapshotDto();
        foreach (var (key, value) in state)
        {
            dto.Enrichments[key] = new EnrichmentEntryDto
            {
                TypeName = value.GetType().Name,
                JsonPayload = JsonConvert.SerializeObject(value, JsonSettings),
            };
        }
        return dto;
    }

    public static Dictionary<string, object> ToDomain(EnrichmentSnapshotDto dto)
    {
        var state = new Dictionary<string, object>();
        foreach (var (key, entry) in dto.Enrichments)
        {
            if (!EnrichmentTypes.TryGetValue(entry.TypeName, out var type))
                continue;
            var value = JsonConvert.DeserializeObject(entry.JsonPayload, type, JsonSettings);
            if (value is not null)
                state[key] = value;
        }
        return state;
    }
}
