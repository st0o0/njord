using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record ParameterTrend(
    [property: JsonProperty("direction")] string Direction,
    [property: JsonProperty("delta")] double Delta);
