using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class HumidityRule : IAlertRule
{
    public AlertType Type => AlertType.Humidity;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var thresholds = settings.Humidity;
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
        return settings.Build(Type, severity, muggiest, details, rounded, effectiveThreshold,
            hoursUntil: muggiest.HoursFrom(now));
    }
}
