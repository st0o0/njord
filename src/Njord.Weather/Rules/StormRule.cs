using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Weather.Alerts.Rules;

public sealed class StormRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Storm;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.Next24Hours(now).HighestBy(p => p.WindGusts) is not { WindGusts: { } gust } peak)
        {
            return Alert.None(Type);
        }

        var (severity, effectiveThreshold) = options.StormGust().Classify(gust);
        var rounded = Math.Round(gust, 1);
        return AlertWindow.Build(Type, severity, peak, new StormDetails(ExpectedMaxGust: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
