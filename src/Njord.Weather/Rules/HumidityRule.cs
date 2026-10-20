using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class HumidityRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Humidity;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.Humidity();
        var day = forecast.NextHoursInclusive(now).Where(p => p is { IsDay: true, DewPoint: not null }).ToList();
        if (day.MaxBy(p => p.DewPoint) is not { DewPoint: { } dewPoint } muggiest)
        {
            return Alert.None(Type);
        }

        var (severity, effectiveThreshold) = thresholds.Classify(dewPoint);
        var comfortLevel = severity switch
        {
            AlertSeverity.Red => HumidityComfort.Tropical,
            AlertSeverity.Orange => HumidityComfort.Oppressive,
            AlertSeverity.Yellow => HumidityComfort.Muggy,
            _ => (HumidityComfort?)null,
        };

        var rounded = Math.Round(dewPoint, 1);
        var details = new HumidityDetails(MaxDewpoint: rounded, ComfortLevel: comfortLevel);
        return AlertWindow.Build(Type, severity, muggiest, details, rounded, effectiveThreshold,
            hoursUntil: muggiest.HoursFrom(now));
    }
}
