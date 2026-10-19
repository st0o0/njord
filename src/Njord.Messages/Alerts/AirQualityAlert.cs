namespace Njord.Messages.Alerts;

public enum AirQualityAlertType
{
    Pollen,
    Pm25,
}

public static class AirQualityAlertTypeExtensions
{
    public static string ToWireName(this AirQualityAlertType type) => type switch
    {
        AirQualityAlertType.Pollen => "pollen",
        AirQualityAlertType.Pm25 => "pm2_5",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}

public sealed record AirQualityAlert(
    AirQualityAlertType Type,
    AlertSeverity Severity,
    double TriggerValue,
    double Threshold,
    int? HoursUntil = null)
{
    public static AirQualityAlert None(AirQualityAlertType type) => new(type, AlertSeverity.None, 0.0, 0.0);
}
