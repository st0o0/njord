using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record StabilityInfo(
    [property: JsonProperty("label")] Stability Stability,
    [property: JsonProperty("ratio")] double Ratio);
