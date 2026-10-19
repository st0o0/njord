using Njord.Messages.Alerts;

namespace Njord.Tests.Shared;

public static class UnifiedAlert
{
    public static Alert Of(AlertType type, AlertSeverity severity, double triggerValue, double threshold, int? hoursUntil = null) =>
        new(type, severity, 1.0, NoDetails.Instance,
            TriggerValue: triggerValue,
            Threshold: threshold,
            HoursUntil: hoursUntil,
            Level: ConfidenceLevel.High);
}
