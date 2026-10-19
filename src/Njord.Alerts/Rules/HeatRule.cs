using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class HeatRule : IAlertRule
{
    public AlertType Type => AlertType.Heat;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var window = forecast.Next24Hours(now);
        var hottestApparent = window.HighestBy(p => p.ApparentTemperature);
        var hottestAir = window.HighestBy(p => p.Temperature);
        if (hottestApparent is null || hottestAir is null)
        {
            return Alert.None(Type);
        }

        var (peak, max) = hottestApparent.ApparentTemperature >= hottestAir.Temperature
            ? (hottestApparent, hottestApparent.ApparentTemperature)
            : (hottestAir, hottestAir.Temperature);

        var (severity, effectiveThreshold) = settings.Heat.Classify(max);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(Type);
        }

        var rounded = Math.Round(max, 1);
        return settings.Build(Type, severity, peak, new HeatDetails(ExpectedMax: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
