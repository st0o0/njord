namespace Njord.Mqtt.Payloads;

public sealed record SnowAlertPayload(double ExpectedAccumulation, double? FreezingLevel, int ModelsAgreeing) : AlertPayload;
