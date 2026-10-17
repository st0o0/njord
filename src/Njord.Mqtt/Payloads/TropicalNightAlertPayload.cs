using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record TropicalNightAlertPayload(
    double NightMinimum,
    [property: JsonPropertyName("hours_above_20")] int HoursAbove20) : AlertPayload;
