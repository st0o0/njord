using Njord.Compute.Contracts;

namespace Njord.Indices;

public sealed record ScoredIndex(int Value, ScoreEnvelope? Envelope);
