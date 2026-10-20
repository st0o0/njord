using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;

namespace Njord.AirQuality;

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
