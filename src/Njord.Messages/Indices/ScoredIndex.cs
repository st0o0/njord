namespace Njord.Messages.Indices;

public sealed record ScoredIndex(int Value, ScoreEnvelope? Envelope);
