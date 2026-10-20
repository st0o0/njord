using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class UvRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Uv";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.EnsembleStdDev,
            OptionalWeather: WeatherParams.UvIndex));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.NextHoursInclusive(now).HighestBy(p => p.UvIndex) is not { UvIndex: { } uvIndex } peak || uvIndex <= 0)
        {
            return Alert.None();
        }

        var (level, severity, uvThreshold) = uvIndex switch
        {
            >= 11 => ("extreme", AlertSeverity.Red, 11.0),
            >= 8 => ("very_high", AlertSeverity.Red, 8.0),
            >= 6 => ("high", AlertSeverity.Orange, 6.0),
            >= 3 => ("moderate", AlertSeverity.Yellow, 3.0),
            _ => ("low", AlertSeverity.None, 0.0),
        };

        var rounded = Math.Round(uvIndex, 1);
        return AlertWindow.Build(severity, peak, new UvDetails(Level: level, UvIndex: rounded),
            rounded, uvThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
