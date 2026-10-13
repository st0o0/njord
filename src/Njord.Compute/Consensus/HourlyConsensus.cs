namespace Njord.Compute.Consensus;

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);
