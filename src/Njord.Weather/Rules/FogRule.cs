using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class FogRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Fog";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.DewPoint | WeatherParams.WindSpeed | WeatherParams.Humidity | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var fogPoints = forecast.NextHoursInclusive(now)
            .Where(p => p.Temperature - p.DewPoint < 2.0 && p is { WindSpeed: < 3.0, Humidity: > 90.0 })
            .ToList();
        var fogHours = fogPoints.Count;
        var severity = fogHours switch
        {
            >= 1 when fogHours >= options.FogPersistentHours => AlertSeverity.Orange,
            >= 1 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var first = fogPoints.FirstOrDefault();
        return AlertWindow.Build(severity, first, new FogDetails(FogHours: fogHours, ModelsAgreeing: 0),
            fogHours, 1,
            hoursUntil: first?.HoursFrom(now),
            durationHours: fogHours > 0 ? fogHours : null);
    }
}
