using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class FrostRule : IAlertRule
{
    public AlertType Type => AlertType.Frost;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var thresholds = settings.Frost;
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

        return settings.Build(Type, severity, coldest, details, low, effectiveThreshold,
            hoursUntil: earliest?.HoursFrom(now));
    }
}
