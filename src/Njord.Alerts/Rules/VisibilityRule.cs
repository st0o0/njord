using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts.Rules;

public sealed class VisibilityRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Visibility";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Visibility | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.Visibility();
        var window = forecast.NextHoursInclusive(now);
        if (window.LowestBy(p => p.Visibility) is not { Visibility: { } low } lowest)
        {
            return Alert.None();
        }

        var hoursBelow = window.Count(p => p.Visibility is { } v && thresholds.CrossesAdvisory(v, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(low, strict: true);
        var rounded = Math.Round(low, 0);
        var details = new VisibilityDetails(MinVisibility: rounded, HoursBelow1000m: hoursBelow);
        return AlertWindow.Build(severity, lowest, details, rounded, effectiveThreshold,
            hoursUntil: lowest.HoursFrom(now));
    }
}
