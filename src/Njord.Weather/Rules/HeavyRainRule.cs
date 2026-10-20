using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class HeavyRainRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.HeavyRain;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var window = forecast.Next24Hours(now);
        if (window.HighestBy(p => p.Precipitation) is not { } wettest)
        {
            return Alert.None(Type);
        }

        var hourlyThreshold = options.HeavyRainHourlyThreshold;
        var dailyThreshold = options.HeavyRainDailyThreshold;
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
        return AlertWindow.Build(Type, severity, wettest, details,
            hourlyExceeds ? Math.Round(maxHourly, 1) : Math.Round(dailySum, 1),
            hourlyExceeds ? hourlyThreshold : dailyThreshold,
            hoursUntil: hourlyExceeds ? wettest.HoursFrom(now) : null);
    }
}
