using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class TropicalNightRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "TropicalNight";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.IsDay | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.TropicalNight();
        var night = forecast.NextHoursInclusive(now).Where(p => p is { IsDay: <= 0.5, Temperature: not null }).ToList();
        if (night.Count == 0)
        {
            return Alert.None();
        }

        if (night.MinBy(p => p.Temperature) is not { Temperature: { } nightLow } coldest)
        {
            return Alert.None();
        }

        var hoursAbove = night.Count(p => p.Temperature is { } t && thresholds.CrossesAdvisory(t, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(nightLow, strict: true);
        var rounded = Math.Round(nightLow, 1);
        var details = new TropicalNightDetails(NightMinimum: rounded, HoursAbove20: hoursAbove);
        return AlertWindow.Build(severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: night[0].HoursFrom(now));
    }
}
