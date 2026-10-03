using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record StabilityInfo(
    [property: JsonProperty("label")] string Label,
    [property: JsonProperty("ratio")] double Ratio);
