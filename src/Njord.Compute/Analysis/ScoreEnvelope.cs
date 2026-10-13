namespace Njord.Compute.Analysis;

public sealed record ScoreEnvelope(
    int Min,
    int Max,
    double Confidence);
