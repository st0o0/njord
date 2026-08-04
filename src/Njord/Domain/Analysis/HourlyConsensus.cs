namespace Njord.Domain.Analysis;

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);
