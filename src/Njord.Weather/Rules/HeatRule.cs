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
        var window = forecast.NextHoursInclusive(now);
        if (window.HighestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } apparent } hottestApparent
            || window.HighestBy(p => p.Temperature) is not { Temperature: { } air } hottestAir)
        {
            return Alert.None(Type);
        }

        var (peak, max) = apparent >= air
            ? (hottestApparent, apparent)
            : (hottestAir, air);

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
