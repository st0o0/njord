using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class HeavyRainRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.HeavyRain;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.RainAlert,
        FeatureCategory.WeatherAlerts,
        "Heavy rain warning from hourly precipitation",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Precipitation | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var window = forecast.NextHoursInclusive(now);
        if (window.HighestBy(p => p.Precipitation) is not { Precipitation: { } peakPrecipitation } wettest)
        {
            return Alert.None(Type);
        }

        var hourlyThreshold = options.HeavyRainHourlyThreshold;
        var dailyThreshold = options.HeavyRainDailyThreshold;
        var hourlyHits = window.Count(p => p.Precipitation >= hourlyThreshold);
        var dailySum = window.Sum(p => p.Precipitation ?? 0.0);
        var maxHourly = Math.Max(0.0, peakPrecipitation);

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
