namespace Njord.Consensus;

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);
