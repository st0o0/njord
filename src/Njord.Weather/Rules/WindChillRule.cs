using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class WindChillRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.WindChill;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.WindChillAlert,
        FeatureCategory.WeatherAlerts,
        "Wind chill warning from apparent temperature",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.ApparentTemperature | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.NextHoursInclusive(now).LowestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } low } coldest)
        {
            return Alert.None(Type);
        }

        var windFactor = Math.Round((coldest.Temperature ?? low) - low, 1);
        var (severity, effectiveThreshold) = options.WindChill().Classify(low);
        var exposureRisk = severity switch
        {
            AlertSeverity.Red => ExposureRisk.Frostbite10Min,
            AlertSeverity.Orange => ExposureRisk.Frostbite30Min,
            _ => (ExposureRisk?)null,
        };

        var rounded = Math.Round(low, 1);
        var details = new WindChillDetails(FeltTemperature: rounded, WindFactor: windFactor, ExposureRisk: exposureRisk);
        return AlertWindow.Build(Type, severity, coldest, details, rounded, effectiveThreshold,
            hoursUntil: coldest.HoursFrom(now));
    }
}
