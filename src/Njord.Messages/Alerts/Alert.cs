namespace Njord.Messages.Alerts;

public sealed record Alert(
    AlertSeverity Severity,
    double Confidence,
    AlertDetails Details,
    double TriggerValue = 0.0,
    double Threshold = 0.0,
    double? PeakValue = null,
    int? HoursUntil = null,
    int? DurationHours = null,
    ConfidenceLevel Level = ConfidenceLevel.Medium)
{
    public static Alert None() => new(AlertSeverity.None, 0.0, NoDetails.Instance);
}
