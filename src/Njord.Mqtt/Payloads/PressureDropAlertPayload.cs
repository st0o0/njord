namespace Njord.Mqtt.Payloads;

public sealed record PressureDropAlertPayload(double MaxDrop, int ModelsAgreeing) : AlertPayload;
