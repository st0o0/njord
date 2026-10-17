namespace Njord.Mqtt.Payloads;

public sealed record ScoreEnvelopePayload(int Min, int Max, double Confidence);
