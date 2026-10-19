using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Weather.Alerts.Rules;

public sealed class WindChillRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.WindChill;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.Next24Hours(now).LowestBy(p => p.ApparentTemperature) is not { } coldest)
        {
            return Alert.None(Type);
        }

        var low = coldest.ApparentTemperature;
        var windFactor = Math.Round(coldest.Temperature - low, 1);
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
