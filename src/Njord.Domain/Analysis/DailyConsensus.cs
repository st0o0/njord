namespace Njord.Domain.Analysis;

public sealed record DailyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffDay);
