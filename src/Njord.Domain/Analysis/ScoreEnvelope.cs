using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record ScoreEnvelope(
    [property: JsonProperty("min")] int Min,
    [property: JsonProperty("max")] int Max,
    [property: JsonProperty("confidence")] double Confidence);
