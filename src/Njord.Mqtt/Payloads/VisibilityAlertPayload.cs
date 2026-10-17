using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record VisibilityAlertPayload(
    double MinVisibility,
    [property: JsonPropertyName("hours_below_1000m")] int HoursBelow1000m) : AlertPayload;
