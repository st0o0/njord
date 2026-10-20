using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class WindChillRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "WindChill";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.ApparentTemperature | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.NextHoursInclusive(now).LowestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } low } coldest)
        {
            return Alert.None();
        }

        var windFactor = Math.Round((coldest.Temperature ?? low) - low, 1);
        var (severity, effectiveThreshold) = options.WindChill().Classify(low);
        var exposureRisk = severity switch
        {
            AlertSeverity.Red => "frostbite_10min",
            AlertSeverity.Orange => "frostbite_30min",
            _ => (string?)null,
        };

        var rounded = Math.Round(low, 1);
        var details = new WindChillDetails(FeltTemperature: rounded, WindFactor: windFactor, ExposureRisk: exposureRisk);
        return AlertWindow.Build(severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: coldest.HoursFrom(now));
    }
}
