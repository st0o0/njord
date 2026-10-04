namespace Njord.Analysis;

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);
