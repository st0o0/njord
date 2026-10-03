using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

[method: JsonConstructor]
public sealed record ConsensusResult(
    [property: JsonProperty("parameters")] IReadOnlyList<ParameterConsensus> Parameters,
    [property: JsonProperty("dailyParameters")] IReadOnlyList<ParameterConsensus> DailyParameters,
    [property: JsonProperty("computedAt")] DateTimeOffset? ComputedAt = null)
{
    public ConsensusResult(IReadOnlyList<ParameterConsensus> parameters)
        : this(parameters, [], null) { }
}
