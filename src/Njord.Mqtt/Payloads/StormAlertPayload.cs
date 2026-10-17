namespace Njord.Mqtt.Payloads;

public sealed record StormAlertPayload(double ExpectedMaxGust, int ModelsAgreeing) : AlertPayload;
