namespace Njord.Messages.Indices;

public sealed record ScoreEnvelope(
    int Min,
    int Max,
    double Confidence);
