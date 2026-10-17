namespace Njord.Mqtt.Payloads;

public sealed record ThunderstormAlertPayload(int ModelsAgreeing) : AlertPayload;
