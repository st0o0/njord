namespace Njord.Compute.Analysis;

public sealed record Alert(
    AlertType Type,
    AlertSeverity Severity,
    double Confidence,
    AlertDetails Details,
    double TriggerValue = 0.0,
    double Threshold = 0.0,
    double? PeakValue = null,
    int? HoursUntil = null,
    int? DurationHours = null)
{
    public static Alert None(AlertType type) =>
        new(type, AlertSeverity.None, 0.0, NoDetails.Instance);
}
