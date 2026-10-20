namespace Njord.Messages.Alerts;

public static class AlertSeverityExtensions
{
    public static string ToWireName(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.None => "none",
        AlertSeverity.Yellow => "yellow",
        AlertSeverity.Orange => "orange",
        AlertSeverity.Red => "red",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };
}
