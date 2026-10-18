using Njord.Messages.Indices;

namespace Njord.Mqtt.Payloads;

public sealed record IndexScorePayload(IndexKind Kind, int Score, ScoreEnvelopePayload? Envelope);
