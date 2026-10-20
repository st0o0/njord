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
        var day = forecast.Next24Hours(now).Where(p => p.IsDay == true).ToList();
        if (day.Count == 0)
        {
            return Alert.None(Type);
        }

        var muggiest = day.MaxBy(p => p.DewPoint)!;
        var (severity, effectiveThreshold) = thresholds.Classify(muggiest.DewPoint);
        var comfortLevel = severity switch
        {
            AlertSeverity.Red => HumidityComfort.Tropical,
            AlertSeverity.Orange => HumidityComfort.Oppressive,
            AlertSeverity.Yellow => HumidityComfort.Muggy,
            _ => (HumidityComfort?)null,
        };

        var rounded = Math.Round(muggiest.DewPoint, 1);
        var details = new HumidityDetails(MaxDewpoint: rounded, ComfortLevel: comfortLevel);
        return AlertWindow.Build(Type, severity, muggiest, details, rounded, effectiveThreshold,
            hoursUntil: muggiest.HoursFrom(now));
    }
}
