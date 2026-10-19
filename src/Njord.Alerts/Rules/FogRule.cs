using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class FogRule : IAlertRule
{
    public AlertType Type => AlertType.Fog;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var fogPoints = forecast.Next24Hours(now)
            .Where(p => p.Temperature - p.DewPoint < 2.0 && p is { WindSpeed: < 3.0, Humidity: > 90.0 })
            .ToList();
        var fogHours = fogPoints.Count;
        var severity = fogHours switch
        {
            >= 1 when fogHours >= settings.FogPersistentHours => AlertSeverity.Orange,
            >= 1 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var first = fogPoints.FirstOrDefault();
        return settings.Build(Type, severity, first, new FogDetails(FogHours: fogHours, ModelsAgreeing: 0),
            fogHours, 1,
            hoursUntil: first?.HoursFrom(now),
            durationHours: fogHours > 0 ? fogHours : null);
    }
}
