using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record ConfidenceIntervalInfo(
    [property: JsonProperty("lower")] double Lower,
    [property: JsonProperty("upper")] double Upper);
