namespace Njord.Compute.Contracts;

public sealed record ScoreEnvelope(
    int Min,
    int Max,
    double Confidence);
