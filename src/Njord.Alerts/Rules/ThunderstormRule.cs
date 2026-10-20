using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts.Rules;

public sealed class ThunderstormRule : IAlertRule<DomainForecast<WeatherPoint>>
{

    public string Name => "Thunderstorm";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Cape | WeatherParams.Precipitation | WeatherParams.WindGusts | WeatherParams.EnsembleStdDev));

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var evaluable = forecast.NextHoursInclusive(now).Where(p => p.Cape is not null && p.WindGusts is not null).ToList();
        if (evaluable.Count == 0)
        {
            return Alert.None();
        }

        var meetAll = evaluable
            .Where(p => p.Cape > options.CapeThreshold
                && p.Precipitation > options.ThunderstormPrecipThreshold
                && p.WindGusts > options.ThunderstormGustThreshold)
            .ToList();
        var share = (double)meetAll.Count / evaluable.Count;
        var severity = share switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var peak = (meetAll.Count > 0 ? meetAll : evaluable).HighestBy(p => p.Cape);
        var maxCape = evaluable.Max(p => p.Cape) ?? 0.0;
        return AlertWindow.Build(severity, peak, new ThunderstormDetails(ModelsAgreeing: 0),
            Math.Round(maxCape, 0), options.CapeThreshold,
            hoursUntil: meetAll.Count > 0 ? meetAll[0].HoursFrom(now) : null);
    }
}
