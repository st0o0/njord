using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class HumidityRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Humidity";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.DewPoint | WeatherParams.IsDay | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.Humidity();
        var day = forecast.NextHoursInclusive(now).Where(p => p is { IsDay: > 0.5, DewPoint: not null }).ToList();
        if (day.MaxBy(p => p.DewPoint) is not { DewPoint: { } dewPoint } muggiest)
        {
            return Alert.None();
        }

        var (severity, effectiveThreshold) = thresholds.Classify(dewPoint);
        var comfortLevel = severity switch
        {
            AlertSeverity.Red => "tropical",
            AlertSeverity.Orange => "oppressive",
            AlertSeverity.Yellow => "muggy",
            _ => (string?)null,
        };

        var rounded = Math.Round(dewPoint, 1);
        var details = new HumidityDetails(MaxDewpoint: rounded, ComfortLevel: comfortLevel);
        return AlertWindow.Build(severity, muggiest, details, rounded, effectiveThreshold,
            hoursUntil: muggiest.HoursFrom(now));
    }
}
