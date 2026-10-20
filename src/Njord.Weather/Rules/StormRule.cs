using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts.Rules;

public sealed class StormRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Storm";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.WindGusts | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        if (forecast.NextHoursInclusive(now).HighestBy(p => p.WindGusts) is not { WindGusts: { } gust } peak)
        {
            return Alert.None();
        }

        var (severity, effectiveThreshold) = options.StormGust().Classify(gust);
        var rounded = Math.Round(gust, 1);
        return AlertWindow.Build(severity, peak, new StormDetails(ExpectedMaxGust: rounded, ModelsAgreeing: 0),
            rounded, effectiveThreshold, hoursUntil: peak.HoursFrom(now));
    }
}
