namespace Njord.Mqtt.Payloads;

public sealed record IceAlertPayload(double ExpectedLow, int RainHours, bool SoilFrozen) : AlertPayload;
