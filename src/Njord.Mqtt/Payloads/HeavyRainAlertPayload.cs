namespace Njord.Mqtt.Payloads;

public sealed record HeavyRainAlertPayload(int HourlyExceedModels, int DailyExceedModels) : AlertPayload;
