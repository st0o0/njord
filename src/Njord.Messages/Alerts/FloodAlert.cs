namespace Njord.Messages.Alerts;

public enum FloodAlertType
{
    FloodRisk,
    FloodRain,
}

public static class FloodAlertTypeExtensions
{
    public static string ToWireName(this FloodAlertType type) => type switch
    {
        FloodAlertType.FloodRisk => "flood_risk",
        FloodAlertType.FloodRain => "flood_rain",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}

public sealed record FloodAlert(
    FloodAlertType Type,
    AlertSeverity Severity,
    double TriggerValue,
    double Threshold,
    int? HoursUntil = null)
{
    public static FloodAlert None(FloodAlertType type) => new(type, AlertSeverity.None, 0.0, 0.0);
}
