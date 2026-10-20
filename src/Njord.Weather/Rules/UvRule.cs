using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class UvRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Uv;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.Next24Hours(now).HighestBy(p => p.UvIndex) is not { UvIndex: { } uvIndex } peak || uvIndex <= 0)
        {
            return Alert.None(Type);
        }

        var (level, severity, uvThreshold) = uvIndex switch
        {
            >= 11 => (UvLevel.Extreme, AlertSeverity.Red, 11.0),
            >= 8 => (UvLevel.VeryHigh, AlertSeverity.Red, 8.0),
            >= 6 => (UvLevel.High, AlertSeverity.Orange, 6.0),
            >= 3 => (UvLevel.Moderate, AlertSeverity.Yellow, 3.0),
            _ => (UvLevel.Low, AlertSeverity.None, 0.0),
        };

        var rounded = Math.Round(uvIndex, 1);
        return AlertWindow.Build(Type, severity, peak, new UvDetails(Level: level, UvIndex: rounded),
            rounded, uvThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
