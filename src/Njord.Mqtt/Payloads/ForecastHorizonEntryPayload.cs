namespace Njord.Mqtt.Payloads;

public sealed record ForecastHorizonEntryPayload(string Key, double? Number, string? Text);
