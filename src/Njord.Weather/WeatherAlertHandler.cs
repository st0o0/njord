using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

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

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

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
