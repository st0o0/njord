namespace Njord.Compute.Analysis;

public sealed record ConsensusResult(
    IReadOnlyList<ParameterConsensus> Parameters,
    IReadOnlyList<ParameterConsensus> DailyParameters,
    DateTimeOffset? ComputedAt = null)
{
    public ConsensusResult(IReadOnlyList<ParameterConsensus> parameters)
        : this(parameters, []) { }
}
