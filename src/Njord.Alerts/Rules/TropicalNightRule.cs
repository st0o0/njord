using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class TropicalNightRule : IAlertRule
{
    public AlertType Type => AlertType.TropicalNight;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var thresholds = settings.TropicalNight;
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
        return settings.Build(Type, severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: night[0].HoursFrom(now));
    }
}
