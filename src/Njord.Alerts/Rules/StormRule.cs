using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class StormRule : IAlertRule
{
    public AlertType Type => AlertType.Storm;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        if (forecast.Next24Hours(now).HighestBy(p => p.WindGusts) is not { WindGusts: { } gust } peak)
        {
            return Alert.None(Type);
        }

        var (severity, effectiveThreshold) = settings.StormGust.Classify(gust);
        var rounded = Math.Round(gust, 1);
        return settings.Build(Type, severity, peak, new StormDetails(ExpectedMaxGust: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
