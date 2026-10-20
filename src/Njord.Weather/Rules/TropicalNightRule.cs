using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class TropicalNightRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.TropicalNight;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.TropicalNight();
        var night = forecast.Next24Hours(now).Where(p => p.IsDay == false).ToList();
        if (night.Count == 0)
        {
            return Alert.None(Type);
        }

        var coldest = night.MinBy(p => p.Temperature)!;
        var hoursAbove = night.Count(p => thresholds.CrossesAdvisory(p.Temperature, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(coldest.Temperature, strict: true);
        var rounded = Math.Round(coldest.Temperature, 1);
        var details = new TropicalNightDetails(NightMinimum: rounded, HoursAbove20: hoursAbove);
        return AlertWindow.Build(Type, severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: night[0].HoursFrom(now));
    }
}
