using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class FrostRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Frost;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.Frost();
        var window = forecast.Next24Hours(now);
        if (window.LowestBy(p => p.Temperature) is not { } coldest)
        {
            return Alert.None(Type);
        }

        var earliest = window.FirstOrDefault(p => thresholds.CrossesAdvisory(p.Temperature));
        var (severity, effectiveThreshold) = thresholds.Classify(coldest.Temperature);
        var low = Math.Round(coldest.Temperature, 1);
        var details = new FrostDetails(
            ExpectedLow: low,
            EarliestFrost: earliest is null ? null : Horizon.Hours(earliest.HoursFrom(now)),
            ModelsAgreeing: 0);

        return AlertWindow.Build(Type, severity, coldest, details, low, effectiveThreshold,
            hoursUntil: earliest?.HoursFrom(now));
    }
}
