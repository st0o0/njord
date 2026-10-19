using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class HeavyRainRule : IAlertRule
{
    public AlertType Type => AlertType.HeavyRain;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var window = forecast.Next24Hours(now);
        if (window.HighestBy(p => p.Precipitation) is not { } wettest)
        {
            return Alert.None(Type);
        }

        var hourlyThreshold = settings.HeavyRainHourlyThreshold;
        var dailyThreshold = settings.HeavyRainDailyThreshold;
        var hourlyHits = window.Count(p => p.Precipitation >= hourlyThreshold);
        var dailySum = window.Sum(p => p.Precipitation);
        var maxHourly = Math.Max(0.0, wettest.Precipitation);

        var dailyExceeds = dailySum >= dailyThreshold;
        var hourlyExceeds = hourlyHits > 0;
        var severity = (hourlyExceeds, dailyExceeds) switch
        {
            (true, true) => AlertSeverity.Red,
            (false, true) => AlertSeverity.Orange,
            (true, false) => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var details = new HeavyRainDetails(HourlyExceedModels: hourlyHits, DailyExceedModels: dailyExceeds ? 1 : 0);
        return settings.Build(Type, severity, wettest, details,
            hourlyExceeds ? Math.Round(maxHourly, 1) : Math.Round(dailySum, 1),
            hourlyExceeds ? hourlyThreshold : dailyThreshold,
            hoursUntil: hourlyExceeds ? wettest.HoursFrom(now) : null);
    }
}
