using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class IceRule : IAlertRule
{
    public AlertType Type => AlertType.Ice;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var iceThreshold = settings.IceThreshold;
        var icy = forecast.Next24Hours(now)
            .Where(p => (p.Rain ?? p.Precipitation) > 0 && p.Temperature <= iceThreshold)
            .ToList();
        if (icy.Count == 0)
        {
            return Alert.None(Type);
        }

        var coldest = icy.MinBy(p => p.Temperature)!;
        var minTemp = coldest.Temperature;
        var soilFrozen = icy.Any(p => p.SoilTemperature is <= 0);
        var severity = (minTemp, soilFrozen) switch
        {
            ( <= 0, true) => AlertSeverity.Red,
            ( <= 0, false) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var rounded = Math.Round(minTemp, 1);
        var details = new IceDetails(ExpectedLow: rounded, RainHours: icy.Count, SoilFrozen: soilFrozen);
        return settings.Build(Type, severity, coldest, details, rounded, iceThreshold,
            hoursUntil: icy[0].HoursFrom(now));
    }
}
