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
        var night = forecast.NextHoursInclusive(now).Where(p => p is { IsDay: false, Temperature: not null }).ToList();
        if (night.Count == 0)
        {
            return Alert.None(Type);
        }

        if (night.MinBy(p => p.Temperature) is not { Temperature: { } nightLow } coldest)
        {
            return Alert.None(Type);
        }

        var hoursAbove = night.Count(p => p.Temperature is { } t && thresholds.CrossesAdvisory(t, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(nightLow, strict: true);
        var rounded = Math.Round(nightLow, 1);
        var details = new TropicalNightDetails(NightMinimum: rounded, HoursAbove20: hoursAbove);
        return AlertWindow.Build(Type, severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: night[0].HoursFrom(now));
    }
}
