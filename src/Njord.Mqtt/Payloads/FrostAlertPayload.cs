namespace Njord.Mqtt.Payloads;

public sealed record FrostAlertPayload(double ExpectedLow, string? EarliestFrost, int ModelsAgreeing) : AlertPayload;
