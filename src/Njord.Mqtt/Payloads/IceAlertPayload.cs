namespace Njord.Mqtt.Payloads;

public sealed record IceAlertPayload(double ExpectedLow, int RainHours, string SoilFrozen) : AlertPayload;
