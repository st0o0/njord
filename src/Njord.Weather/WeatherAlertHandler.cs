using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Alerts;

public sealed class WeatherAlertHandler : IDomainHandler
{
    private readonly IReadOnlyList<IAlertRule<DomainForecast<WeatherPoint>>> _rules;
    private readonly IOptions<AlertOptions> _options;

    public WeatherAlertHandler(IEnumerable<IAlertRule<DomainForecast<WeatherPoint>>> rules, IOptions<AlertOptions> options)
    {
        _rules = [.. rules.OrderBy(rule => rule.Type)];
        _options = options;
    }

    public string ConfigKey => "WeatherAlerts";

    public DomainRequirements Requirements { get; } = new(Weather: WeatherParams.Temperature | WeatherParams.ApparentTemperature | WeatherParams.WindSpeed
            | WeatherParams.Precipitation | WeatherParams.Humidity | WeatherParams.DewPoint | WeatherParams.Pressure
            | WeatherParams.WindGusts | WeatherParams.Visibility | WeatherParams.Cape
            | WeatherParams.Snowfall | WeatherParams.FreezingLevelHeight | WeatherParams.IsDay
            | WeatherParams.EnsembleStdDev,
        OptionalWeather: WeatherParams.Rain | WeatherParams.UvIndex | WeatherParams.SoilTemperature);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        var options = _options.Value;
        if (!options.Enabled || current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. _rules
                .Select(rule => rule.Evaluate(weather, options, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new PipelineEvent.AlertRaised(current.Location, alert, now)),
        ];
    }
}
