using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class HeatRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Heat";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.ApparentTemperature | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var window = forecast.NextHoursInclusive(now);
        if (window.HighestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } apparent } hottestApparent
            || window.HighestBy(p => p.Temperature) is not { Temperature: { } air } hottestAir)
        {
            return Alert.None();
        }

        var (peak, max) = apparent >= air
            ? (hottestApparent, apparent)
            : (hottestAir, air);

        var (severity, effectiveThreshold) = options.Heat().Classify(max);
        if (severity == AlertSeverity.None)
        {
            return Alert.None();
        }

        var rounded = Math.Round(max, 1);
        return AlertWindow.Build(severity, peak, new HeatDetails(ExpectedMax: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
