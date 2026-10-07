namespace Njord.Compute.Analysis;

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);
