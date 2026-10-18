namespace Njord.Consensus;

public sealed record DailyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffDay);
