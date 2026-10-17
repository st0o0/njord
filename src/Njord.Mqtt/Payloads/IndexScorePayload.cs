using Njord.Compute.Contracts;

namespace Njord.Mqtt.Payloads;

public sealed record IndexScorePayload(IndexKind Kind, int Score, ScoreEnvelopePayload? Envelope);
