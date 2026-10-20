using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class HeatRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Heat;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
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

        var (severity, effectiveThreshold) = options.Heat().Classify(max);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(Type);
        }

        var rounded = Math.Round(max, 1);
        return AlertWindow.Build(Type, severity, peak, new HeatDetails(ExpectedMax: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
