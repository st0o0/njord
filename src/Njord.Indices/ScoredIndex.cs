using Njord.Messages.Indices;

namespace Njord.Indices;

public sealed record ScoredIndex(int Value, ScoreEnvelope? Envelope);
