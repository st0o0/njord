namespace Njord.Mqtt.Payloads;

public sealed record FogAlertPayload(int FogHours, int ModelsAgreeing) : AlertPayload;
