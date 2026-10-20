using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts.Rules;

public sealed class FrostRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Frost";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.Frost();
        var window = forecast.NextHoursInclusive(now);
        if (window.LowestBy(p => p.Temperature) is not { Temperature: { } lowest } coldest)
        {
            return Alert.None();
        }

        var earliest = window.FirstOrDefault(p => p.Temperature is { } t && thresholds.CrossesAdvisory(t));
        var (severity, effectiveThreshold) = thresholds.Classify(lowest);
        var low = Math.Round(lowest, 1);
        var details = new FrostDetails(
            ExpectedLow: low,
            EarliestFrost: earliest is null ? null : Horizon.Hours(earliest.HoursFrom(now)),
            ModelsAgreeing: 0);

        return AlertWindow.Build(severity, coldest, details, low, effectiveThreshold,
            hoursUntil: earliest?.HoursFrom(now));
    }
}
