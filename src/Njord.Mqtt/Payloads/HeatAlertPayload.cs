namespace Njord.Mqtt.Payloads;

public sealed record HeatAlertPayload(double ExpectedMax, int ModelsAgreeing) : AlertPayload;
