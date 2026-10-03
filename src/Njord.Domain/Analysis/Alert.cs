using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record Alert(
    [property: JsonProperty("type")] AlertType Type,
    [property: JsonProperty("severity")] AlertSeverity Severity,
    [property: JsonProperty("confidence")] double Confidence,
    [property: JsonProperty("attributes")] IReadOnlyDictionary<string, object?> Attributes,
    [property: JsonProperty("triggerValue")] double TriggerValue = 0.0,
    [property: JsonProperty("threshold")] double Threshold = 0.0,
    [property: JsonProperty("peakValue")] double? PeakValue = null,
    [property: JsonProperty("hoursUntil")] int? HoursUntil = null,
    [property: JsonProperty("durationHours")] int? DurationHours = null)
{
    public static Alert None(AlertType type) =>
        new(type, AlertSeverity.None, 0.0, new Dictionary<string, object?>());
}
