using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Shared;

internal static class AirQualityAlertWindow
{
    public static AlertSeverity SeverityFor(double value, double yellow, double orange, double red) => value switch
    {
        _ when value >= red => AlertSeverity.Red,
        _ when value >= orange => AlertSeverity.Orange,
        _ when value >= yellow => AlertSeverity.Yellow,
        _ => AlertSeverity.None,
    };
}
