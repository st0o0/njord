using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class UvRule : IAlertRule
{
    public AlertType Type => AlertType.Uv;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        if (forecast.Next24Hours(now).HighestBy(p => p.UvIndex) is not { } peak || peak.UvIndex <= 0)
        {
            return Alert.None(Type);
        }

        var (level, severity, uvThreshold) = peak.UvIndex switch
        {
            >= 11 => (UvLevel.Extreme, AlertSeverity.Red, 11.0),
            >= 8 => (UvLevel.VeryHigh, AlertSeverity.Red, 8.0),
            >= 6 => (UvLevel.High, AlertSeverity.Orange, 6.0),
            >= 3 => (UvLevel.Moderate, AlertSeverity.Yellow, 3.0),
            _ => (UvLevel.Low, AlertSeverity.None, 0.0),
        };

        var rounded = Math.Round(peak.UvIndex, 1);
        return settings.Build(Type, severity, peak, new UvDetails(Level: level, UvIndex: rounded),
            rounded, uvThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
