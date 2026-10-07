using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record ParameterTrend(
    [property: JsonProperty("direction")] string Direction,
    [property: JsonProperty("delta")] double Delta);
