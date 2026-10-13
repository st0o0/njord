using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record ParameterTrend(
    [property: JsonProperty("direction")] TrendDirection Direction,
    [property: JsonProperty("delta")] double Delta);
