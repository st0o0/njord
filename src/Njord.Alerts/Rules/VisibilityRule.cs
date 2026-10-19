using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class VisibilityRule : IAlertRule
{
    public AlertType Type => AlertType.Visibility;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var thresholds = settings.Visibility;
        var window = forecast.Next24Hours(now);
        if (window.LowestBy(p => p.Visibility) is not { Visibility: { } low } lowest)
        {
            return Alert.None(Type);
        }

        var hoursBelow = window.Count(p => p.Visibility is { } v && thresholds.CrossesAdvisory(v, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(low, strict: true);
        var rounded = Math.Round(low, 0);
        var details = new VisibilityDetails(MinVisibility: rounded, HoursBelow1000m: hoursBelow);
        return settings.Build(Type, severity, lowest, details, rounded, effectiveThreshold,
            hoursUntil: lowest.HoursFrom(now));
    }
}
