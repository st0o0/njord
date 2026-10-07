namespace Njord.Compute.Analysis;

public sealed record DailyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffDay);
